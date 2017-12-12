﻿using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace UnitTests
{
    [TestFixture, Timeout(5000), Ignore("till the router uses the admin queue")]
    public class NonTransactionalRouterTests
    {
        static readonly string inputQueuePath = $".\\private$\\{nameof(NonTransactionalRouterTests)}.Input";
        static readonly string adminQueuePath = $".\\private$\\{nameof(NonTransactionalRouterTests)}.Admin";
        static readonly string outputQueuePath1 = $".\\private$\\{nameof(NonTransactionalRouterTests)}.Output.1";
        static readonly string outputQueuePath2 = $".\\private$\\{nameof(NonTransactionalRouterTests)}.Output.2";
        string inputQueueFormatName;
        string adminQueueFormatName;
        string deadQueueFormatName;
        string outputQueueFormatName1;
        string outputQueueFormatName2;
        Queue input;
        Queue dead;
        Queue out1;
        Queue out2;
        Sender sender;

        [SetUp]
        public void Setup()
        {
            inputQueueFormatName = Queue.TryCreate(inputQueuePath, QueueTransactional.Transactional);
            adminQueueFormatName = Queue.TryCreate(adminQueuePath, QueueTransactional.None);
            outputQueueFormatName1 = Queue.TryCreate(outputQueuePath1, QueueTransactional.Transactional);
            outputQueueFormatName2 = Queue.TryCreate(outputQueuePath2, QueueTransactional.Transactional);
            deadQueueFormatName = $"{inputQueueFormatName };Poison";

            using (var q = Queue.Open(inputQueueFormatName, QueueAccessMode.Receive))
                q.Purge();
            using (var q = Queue.Open(adminQueueFormatName, QueueAccessMode.Receive))
                q.Purge();
            using (var q = Queue.Open(outputQueueFormatName1, QueueAccessMode.Receive))
                q.Purge();
            using (var q = Queue.Open(outputQueueFormatName2, QueueAccessMode.Receive))
                q.Purge();

            input = Queue.Open(inputQueueFormatName, QueueAccessMode.Send);
            dead = Queue.Open(deadQueueFormatName, QueueAccessMode.Receive);
            out1 = Queue.Open(outputQueueFormatName1, QueueAccessMode.Receive);
            out2 = Queue.Open(outputQueueFormatName2, QueueAccessMode.Receive);
            sender = new Sender(adminQueueFormatName);
        }

        [Test]
        public async Task can_route_non_transactional()
        {
            using (var router = new NonTransactionalRouter(inputQueueFormatName, sender, msg => msg.Label.Contains("1") ? out1 : out2))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Post(new Message { Label = "1", AppSpecific = 1 });
                    var got = out1.Receive();
                    Assert.AreEqual("1", got.Label);
                }
                finally
                {
                    await router.StopAsync();
                }
            }
        }

        [Test]
        public async Task can_route_non_transactional_to_other_queue()
        {
            using (var router = new NonTransactionalRouter(inputQueueFormatName, sender, msg => msg.Label.Contains("1") ? out1 : out2))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Post(new Message { Label = "2", AppSpecific = 1 });
                    var got = out2.Receive();
                    Assert.AreEqual("2", got.Label);
                }
                finally
                {
                    await router.StopAsync();
                }
            }
        }

        [Test]
        public async Task can_route_non_transactional_to_deadletter()
        {
            using (var router = new NonTransactionalRouter(inputQueueFormatName, sender, msg => null))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Post(new Message { Label = "3", AppSpecific = 1 });
                    var got = dead.Receive();
                    Assert.AreEqual("3", got.Label);
                }
                finally
                {
                    await router.StopAsync();
                }
            }
        }
    }
}
