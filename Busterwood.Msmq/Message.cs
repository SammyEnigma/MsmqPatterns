﻿using System;
using System.Diagnostics.Contracts;
using System.Linq;

namespace BusterWood.Msmq
{
    /// <summary>A message that can be send to a <see cref="Queue"/> or received from a <see cref="Queue"/></summary>
    public class Message
    {
        internal readonly MessageProperties Props;

        internal const int GenericIdSize = 16;
        internal const int MessageIdSize = 20;

        public Message() : this(new MessageProperties())
        {
            // always add Id so we can read it after send
            Props.SetByteArray(Native.MESSAGE_PROPID_MSGID, new byte[MessageIdSize]);
        }

        internal Message(MessageProperties properties)
        {
            Contract.Requires(properties != null);
            Props = properties;
        }     

        /// <summary>The identifier of this message.  Only set after the message has been sent.</summary>
        public MessageId Id
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_MSGID))
                    return new MessageId();
                var bytes = Props.GetByteArray(Native.MESSAGE_PROPID_MSGID);
                return new MessageId(bytes);
            }
        }

        /// <summary>The type of acknowledgement messages that should be sent to the <see cref="AdministrationQueue"/></summary>
        public AcknowledgmentTypes AcknowledgmentTypes
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_ACKNOWLEDGE))
                    return AcknowledgmentTypes.None;

                return (AcknowledgmentTypes)Props.GetByte(Native.MESSAGE_PROPID_ACKNOWLEDGE);
            }
            set
            {
                if (value == AcknowledgmentTypes.None)
                {
                    Props.Remove(Native.MESSAGE_PROPID_ACKNOWLEDGE);
                }
                else
                {
                    Props.SetByte(Native.MESSAGE_PROPID_ACKNOWLEDGE, (byte)value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the format name of the queue that administration messages are sent too.
        /// Administration messages will have <see cref="Class"/> of <see cref="MessageClass.Report"/> combined with other <see cref="MessageClass"/> flags.
        /// </summary>
        public string AdministrationQueue
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_ADMIN_QUEUE))
                    return "";

                var len = Props.GetUInt(Native.MESSAGE_PROPID_ADMIN_QUEUE_LEN);
                return len == 0 ? "" : Native.StringFromBytes(Props.GetString(Native.MESSAGE_PROPID_ADMIN_QUEUE), len);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Props.Remove(Native.MESSAGE_PROPID_ADMIN_QUEUE);
                    Props.Remove(Native.MESSAGE_PROPID_ADMIN_QUEUE_LEN);
                }
                else
                {
                    Props.SetString(Native.MESSAGE_PROPID_ADMIN_QUEUE, value);
                    Props.SetUInt(Native.MESSAGE_PROPID_ADMIN_QUEUE_LEN, value.Length);
                }
            }
        }

        /// <summary>User defined integer data</summary>
        public int AppSpecific
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_APPSPECIFIC))
                    return 0;
                return Props.GetUInt(Native.MESSAGE_PROPID_APPSPECIFIC);
            }
            set
            {
                if (value == 0)
                    Props.Remove(Native.MESSAGE_PROPID_APPSPECIFIC);
                else
                    Props.SetUInt(Native.MESSAGE_PROPID_APPSPECIFIC, value);
            }
        }

        /// <summary>When the message arrived, to the nearest second</summary>
        public DateTime? ArrivedTime
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_ARRIVEDTIME))
                    return null;

                DateTime time = new DateTime(1970, 1, 1);
                time = time.AddSeconds(Props.GetUInt(Native.MESSAGE_PROPID_ARRIVEDTIME)).ToLocalTime();
                return time;
            }
        }
        
        /// <summary>The main payload of the message. <see cref="BodyType"/> defines the data type</summary>
        public byte[] Body
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_BODY))
                    return null;
                var len = Props.GetUInt(Native.MESSAGE_PROPID_BODY_SIZE);
                if (len == 0)
                    return null;
                return Props.GetByteArray(Native.MESSAGE_PROPID_BODY);
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (value.Length == 0)
                {
                    Props.Remove(Native.MESSAGE_PROPID_BODY);
                    Props.Remove(Native.MESSAGE_PROPID_BODY_SIZE);
                }
                else
                {
                    Props.SetByteArray(Native.MESSAGE_PROPID_BODY, value);
                    Props.SetUInt(Native.MESSAGE_PROPID_BODY_SIZE, value.Length);
                }
            }
        }

        /// <summary>The type of the <see cref="Body"/></summary>
        public BodyType BodyType
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_BODY_TYPE))
                    return 0;
                return (BodyType)Props.GetUInt(Native.MESSAGE_PROPID_BODY_TYPE);
            }
            set
            {
                Props.SetUInt(Native.MESSAGE_PROPID_BODY_TYPE, (int)value);
            }
        }

        /// <summary>The class of this message</summary>
        public MessageClass Class
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_CLASS))
                    return MessageClass.Normal;

                return (MessageClass)Props.GetUShort(Native.MESSAGE_PROPID_CLASS);
            }
        }

        /// <summary>used to match requests and replies.  Processes that handle requests with <see cref="ResponseQueue"/> set should reply with a message with <see cref="CorrelationId"/> set to the <see cref="Id"/> of the request message</summary>
        public MessageId CorrelationId
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_CORRELATIONID))
                    return new MessageId();
                var id = Props.GetByteArray(Native.MESSAGE_PROPID_CORRELATIONID);
                return new MessageId(id);
            }
            set
            {
                if (value == MessageId.None)
                    Props.Remove(Native.MESSAGE_PROPID_CORRELATIONID);
                else
                    Props.SetByteArray(Native.MESSAGE_PROPID_CORRELATIONID, value.bytes);
            }
        }

        /// <summary>Is this an <see cref="BusterWood.Msmq.Delivery.Express"/> or <see cref="BusterWood.Msmq.Delivery.Recoverable"/> message?</summary>
        public Delivery Delivery
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_DELIVERY))
                    return Delivery.Express;
                return (Delivery)Props.GetByte(Native.MESSAGE_PROPID_DELIVERY);
            }
            set
            {
                if (value == Delivery.Express)
                    Props.Remove(Native.MESSAGE_PROPID_DELIVERY);
                else
                    Props.SetByte(Native.MESSAGE_PROPID_DELIVERY, (byte)Delivery.Recoverable);
            }
        }

        /// <summary>Gets format name of the original destination queue.  Returns empty string when message has not been sent.</summary>
        public string DestinationQueue
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_DEST_QUEUE))
                    return "";

                var len = Props.GetUInt(Native.MESSAGE_PROPID_DEST_QUEUE_LEN);
                return len == 0 ? "" : Native.StringFromBytes(Props.GetString(Native.MESSAGE_PROPID_DEST_QUEUE), len);
            }
        }

        /// <summary>User defined header information sent with the message</summary>
        public byte[] Extension
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_EXTENSION))
                    return null;
                var len = Props.GetUInt(Native.MESSAGE_PROPID_EXTENSION_LEN);
                if (len == 0)
                    return null;
                var data = Props.GetByteArray(Native.MESSAGE_PROPID_EXTENSION);
                if (data.Length == len)
                    return data;
                var retVal = new byte[len];
                Array.Copy(data, retVal, len);
                Props.SetByteArray(Native.MESSAGE_PROPID_EXTENSION, retVal); // store it back in case the property is read more than once
                return retVal;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (value.Length == 0)
                {
                    Props.Remove(Native.MESSAGE_PROPID_EXTENSION);
                    Props.Remove(Native.MESSAGE_PROPID_EXTENSION_LEN);
                }
                else
                {
                    Props.SetByteArray(Native.MESSAGE_PROPID_EXTENSION, value);
                    Props.SetUInt(Native.MESSAGE_PROPID_EXTENSION_LEN, value.Length);
                }
            }
        }
        /// <summary><see cref="Msmq.Journal.Journal"/> the message and/or use the <see cref="Msmq.Journal.DeadLetter"/> queue?</summary>
        public Journal Journal
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_JOURNAL))
                    return Journal.None;
                return (Journal)Props.GetByte(Native.MESSAGE_PROPID_JOURNAL);
            }
            set
            {
                if (value == Journal.None)
                    Props.Remove(Native.MESSAGE_PROPID_JOURNAL);
                else
                    Props.SetByte(Native.MESSAGE_PROPID_JOURNAL, (byte)value);
            }
        }

        /// <summary>Gets or sets the label of the message, much like the subject of an e-mail.</summary>
        public string Label
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_LABEL_LEN))
                    return "";
                var labelLen = Props.GetUInt(Native.MESSAGE_PROPID_LABEL_LEN);
                return labelLen == 0 ? "" : Native.StringFromBytes(Props.GetString(Native.MESSAGE_PROPID_LABEL), labelLen);
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (value.Length > 250)
                    throw new ArgumentOutOfRangeException(nameof(value), "Maximum label length is 250");

                if (value.Length == 0)
                {
                    Props.Remove(Native.MESSAGE_PROPID_LABEL);
                    Props.Remove(Native.MESSAGE_PROPID_LABEL_LEN);
                }
                else
                {
                    Props.SetString(Native.MESSAGE_PROPID_LABEL, value);
                    Props.SetUInt(Native.MESSAGE_PROPID_LABEL_LEN, value.Length);
                }
            }
        }

        /// <summary>The queue specific ID of this message.  This property is set once the message has been sent.</summary>
        public long LookupId
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_LOOKUPID))
                    return 0;
                return Props.GetULong(Native.MESSAGE_PROPID_LOOKUPID);
            }
        }

        /// <summary>The priority of the message.  Note that this property is automatically set to <see cref="Priority.Lowest"/> for transactional messages</summary>
        public Priority Priority
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_PRIORITY))
                    return Priority.Normal;
                return (Priority)Props.GetByte(Native.MESSAGE_PROPID_PRIORITY);
            }
            set
            {
                if (value == Priority.Normal)
                    Props.Remove(Native.MESSAGE_PROPID_PRIORITY);
                else
                    Props.SetByte(Native.MESSAGE_PROPID_PRIORITY, (byte)value);
            }
        }

        /// <summary>Gets or sets the format name of the response queue</summary>
        public string ResponseQueue
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_RESP_QUEUE))
                    return null;
                var len = Props.GetUInt(Native.MESSAGE_PROPID_RESP_QUEUE_LEN);
                return len == 0 ? "" : Native.StringFromBytes(Props.GetString(Native.MESSAGE_PROPID_RESP_QUEUE), len);
            }
            set
            {
                if (value == null)
                {
                    Props.Remove(Native.MESSAGE_PROPID_RESP_QUEUE);
                    Props.Remove(Native.MESSAGE_PROPID_RESP_QUEUE_LEN);
                }
                else
                {
                    Props.SetString(Native.MESSAGE_PROPID_RESP_QUEUE, value);
                    Props.SetUInt(Native.MESSAGE_PROPID_RESP_QUEUE_LEN, value.Length);
                }
            }
        }

        /// <summary>When the message was sent, to the nearest second</summary>
        public DateTime? SentTime
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_SENTTIME))
                    return null;

                var st = Props.GetUInt(Native.MESSAGE_PROPID_SENTTIME);
                DateTime time = new DateTime(1970, 1, 1);
                time = time.AddSeconds(st).ToLocalTime();
                return time;
            }
        }

        /// <summary>The maximum amount of time a message is allowed to be queued before being received</summary>
        public TimeSpan? TimeToBeReceived
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_TIME_TO_BE_RECEIVED))
                    return null;

                var secs = (uint)Props.GetUInt(Native.MESSAGE_PROPID_TIME_TO_BE_RECEIVED);
                return secs == uint.MaxValue ? (TimeSpan?)null : TimeSpan.FromSeconds(secs);
            }
            set
            {
                if (value == null)
                {
                    Props.Remove(Native.MESSAGE_PROPID_TIME_TO_BE_RECEIVED);
                    return;
                }

                long timeoutInSeconds = (long)value.Value.TotalSeconds;
                if (timeoutInSeconds < 0)
                    throw new ArgumentException("Cannot be negative", nameof(value));

                if (timeoutInSeconds >= uint.MaxValue)
                    Props.Remove(Native.MESSAGE_PROPID_TIME_TO_BE_RECEIVED);
                else
                    Props.SetUInt(Native.MESSAGE_PROPID_TIME_TO_BE_RECEIVED, (int)((uint)timeoutInSeconds));
            }
        }

        /// <summary>The maximum amount of time a message is allowed before reaching the destination queue. Defaults to a system defined setting, typically 4 days</summary>
        public TimeSpan? TimeToReachQueue
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_TIME_TO_REACH_QUEUE))
                    return null;

                var secs = (uint)Props.GetUInt(Native.MESSAGE_PROPID_TIME_TO_REACH_QUEUE);
                return secs == uint.MaxValue ? (TimeSpan?)null : TimeSpan.FromSeconds(secs);
            }
            set
            {
                if (value == null)
                {
                    Props.Remove(Native.MESSAGE_PROPID_TIME_TO_REACH_QUEUE);
                    return;
                }

                long timeoutInSeconds = (long)value.Value.TotalSeconds;
                if (timeoutInSeconds < 0)
                    throw new ArgumentException("Cannot be negative", nameof(value));

                if (timeoutInSeconds >= uint.MaxValue)
                    Props.Remove(Native.MESSAGE_PROPID_TIME_TO_REACH_QUEUE);
                else
                    Props.SetUInt(Native.MESSAGE_PROPID_TIME_TO_REACH_QUEUE, (int)((uint)timeoutInSeconds));
            }
        }

        /// <summary>Gets the Id of the transaction that this message was sent as part of</summary>
        public string TransactionId
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_XACTID))
                    return "";
                var buf = Props.GetByteArray(Native.MESSAGE_PROPID_XACTID);
                return buf.Any(b => b != 0) ? IdFromByteArray(buf) : "";
            }
        }

        private string IdFromByteArray(byte[] bytes)
        {
            byte[] guidBytes = new byte[GenericIdSize];
            Array.Copy(bytes, guidBytes, GenericIdSize);
            int id = BitConverter.ToInt32(bytes, GenericIdSize);
            var guid = new Guid(guidBytes);
            return $"{guid}\\{id}";
        }

        /// <summary>Is the message the first message in a transaction</summary>
        public bool TransactionFirst
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_FIRST_IN_XACT))
                    return false;
                return Props.GetByte(Native.MESSAGE_PROPID_FIRST_IN_XACT) == 1;
            }
        }
        
        /// <summary>Is the message the last message in a transaction</summary>
        public bool TransactionLast
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_LAST_IN_XACT))
                    return false;
                return Props.GetByte(Native.MESSAGE_PROPID_LAST_IN_XACT) == 1;
            }
        }

        /// <summary>Indicates the number of times that transactional processing has been aborted on a message since it was placed in its current queue.</summary>
        /// <remarks>Moving a message to/from a subqueue resets this count</remarks>
        public int TransactionAbortCount
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_ABORT_COUNT))
                    return 0;
                return Props.GetUInt(Native.MESSAGE_PROPID_ABORT_COUNT);
            }
        }

        /// <summary>Indicates the number of times that transactional processing has been aborted on a message during its lifetime.</summary>
        public int TransactionMoveCount
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_MOVE_COUNT))
                    return 0;
                return Props.GetUInt(Native.MESSAGE_PROPID_MOVE_COUNT);
            }
        }

        /// <summary>Attach the sender Id to the message?  The default is <see cref="SenderIdType.Sid"/> which is 2.5 times slower than <see cref="SenderIdType.None"/></summary>
        public SenderIdType SenderIdType
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_SENDERID_TYPE))
                    return SenderIdType.Sid;
                return (SenderIdType)Props.GetUInt(Native.MESSAGE_PROPID_SENDERID_TYPE);
            }
            set
            {
                if (value == SenderIdType.Sid)
                    Props.Remove(Native.MESSAGE_PROPID_SENDERID_TYPE);
                else
                    Props.SetUInt(Native.MESSAGE_PROPID_SENDERID_TYPE, (int)value);
            }
        }
    }



}