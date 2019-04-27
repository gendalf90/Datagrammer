using System;
using System.Runtime.Serialization;

namespace Datagrammer
{
    internal class ProcessingStoppedException : Exception
    {
        public ProcessingStoppedException()
        {
        }

        public ProcessingStoppedException(string message) : base(message)
        {
        }

        public ProcessingStoppedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ProcessingStoppedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
