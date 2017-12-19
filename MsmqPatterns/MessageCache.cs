﻿using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using BusterWood.Caching;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>
    /// This caches reads all messages from the <see cref="InputQueueFormatName"/> and either stores them or sends a response message, depending on the <see cref="Message.Label"/>.
    /// If the <see cref="Message.Label"/> starts with <see cref="LastPrefix"/> then the <see cref="LastPrefix"/> is removed from the label
    /// and the result used to lookup a message in the cache. The found message, or an empty message if not in the cache, is then sent
    /// to the input message's <see cref="Message.ResponseQueue"/>.
    /// </summary>
    public class MessageCache : IProcessor
    {
        public string LastPrefix { get; set; } = "last.";
        readonly QueueCache<QueueWriter> _queueCache;
        Cache<string, Message> _cache;
        QueueReader _input;
        QueueReader _admin;
        Task _mainTask;
        Task _adminTask;

        public MessageCache(string inputQueueFormatName, string adminQueueFormatName, int? gen0Limit, TimeSpan? timeToLive)
        {
            Contract.Requires(!string.IsNullOrEmpty(inputQueueFormatName));
            Contract.Requires(!string.IsNullOrEmpty(adminQueueFormatName));
            if (Queue.IsTransactional(inputQueueFormatName) != QueueTransactional.None)
                throw new ArgumentException(inputQueueFormatName + " must be non-transactional");
            if (Queue.IsTransactional(adminQueueFormatName) != QueueTransactional.None)
                throw new ArgumentException(adminQueueFormatName + " must be non-transactional");

            InputQueueFormatName = inputQueueFormatName;
            AdminQueueFormatName = adminQueueFormatName;
            Gen0Limit = gen0Limit;
            TimeToLive = timeToLive;
            _queueCache = new QueueCache<QueueWriter>((fn, mode, share) => new QueueWriter(fn));
        }

        public string InputQueueFormatName { get; }
        public string AdminQueueFormatName { get; }
        public int? Gen0Limit { get; }
        public TimeSpan? TimeToLive { get; }

        public void Dispose()
        {
            try
            {
                StopAsync().Wait();
            }
            catch
            {
                // dispose should never throw exceptions
            }
        }

        public Task<Task> StartAsync()
        {
            _input = new QueueReader(InputQueueFormatName);
            _admin = new QueueReader(AdminQueueFormatName);
            _cache = new Cache<string, Message>(Gen0Limit, TimeToLive);
            _mainTask = RunAsync();
            _adminTask = AdminAsync();
            return Task.FromResult(_mainTask);
        }

        async Task RunAsync()
        {
            try
            {
                for (;;)
                {
                    var msg = _input.Read(Properties.All, TimeSpan.Zero) ?? await _input.ReadAsync(Properties.All);
                    if (msg.Label.StartsWith(LastPrefix, StringComparison.OrdinalIgnoreCase))
                        SendLastValue(msg);
                    else
                        StoreLastValue(msg);
                }

            }
            catch (ObjectDisposedException)
            {
                // Stop was called
            }
            catch (QueueException ex) when (ex.ErrorCode == ErrorCode.OperationCanceled)
            {
                // Stop was called
            }
        }

        private void SendLastValue(Message msg)
        {
            var key = msg.Label.Substring(LastPrefix.Length);
            if (string.IsNullOrWhiteSpace(msg.ResponseQueue))
            {
                Console.Error.WriteLine($"INFO: Received a request for '{key}' without the response queue being set");
                return;
            }

            var last = _cache[key];
            if (last == null)
            {
                Console.Error.WriteLine($"DEBUG: there is no cached value for '{key}', sending an empty message");
                last = new Message { Label = key };
            }

            var replyQueue = _queueCache.Open(msg.ResponseQueue, QueueAccessMode.Send);
            last.CorrelationId = msg.Id;
            replyQueue.Write(last); 
            Console.Error.WriteLine($"DEBUG: sent reply for '{key}' to {msg.ResponseQueue}");
            // note: we do not wait for confirmation of delivery, we just report errors on via the AdminAsync (_adminTask)
        }

        private void StoreLastValue(Message msg)
        {
            _cache[msg.Label] = msg;
            Console.Error.WriteLine($"DEBUG: stored message for '{msg.Label}'");
        }

        async Task AdminAsync()
        {
            try
            {
                for (;;)
                {
                    const Properties adminProps = Properties.Class | Properties.Id | Properties.Label | Properties.DestinationQueue;
                    var msg = _admin.Read(adminProps, TimeSpan.Zero) ?? await _admin.ReadAsync(adminProps);
                    var ack = msg.Acknowledgement();
                    switch (ack)
                    {
                        case MessageClass.ReachQueueTimeout:
                        case MessageClass.AccessDenied:
                        case MessageClass.BadDestinationQueue:
                        case MessageClass.BadEncryption:
                        case MessageClass.BadSignature:
                        case MessageClass.CouldNotEncrypt:
                        case MessageClass.HopCountExceeded:
                        case MessageClass.NotTransactionalMessage:
                        case MessageClass.NotTransactionalQueue:
                        case MessageClass.Deleted:
                        case MessageClass.QueueDeleted:
                        case MessageClass.QueuePurged:
                        case MessageClass.QueueExceedQuota:
                            Console.Error.WriteLine($"WARNING: message labelled '{msg.Label}' failed to reach '{msg.DestinationQueue}' because {ack}");
                            break;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Stop was called
            }
            catch (QueueException ex) when (ex.ErrorCode == ErrorCode.OperationCanceled)
            {
                // Stop was called
            }
        }

        public Task StopAsync()
        {
            _input?.Dispose();
            _admin?.Dispose();
            return Task.WhenAll(_mainTask, _adminTask);
        }
    }
}