﻿using BusterWood.Msmq;
using System;
using System.Runtime.Serialization;

namespace BusterWood.MsmqPatterns
{
    [Serializable]
    public class AcknowledgmentException : Exception
    {
        public MessageClass Acknowledgment { get; }

        public AcknowledgmentException()
        {
        }

        public AcknowledgmentException(string message) : base(message)
        {
        }

        public AcknowledgmentException(string message, MessageClass @class) : base(message)
        {
            this.Acknowledgment = @class;
        }

        public AcknowledgmentException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AcknowledgmentException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}