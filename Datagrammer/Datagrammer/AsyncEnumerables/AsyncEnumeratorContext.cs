using System;
using System.Net;

namespace Datagrammer.AsyncEnumerables
{
    public sealed class AsyncEnumeratorContext
    {
        public Memory<byte> Buffer { get; set; }

        public IPEndPoint EndPoint { get; set; }

        public Exception Error { get; set; }
    }
}
