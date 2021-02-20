using System;
using System.Net;

namespace Datagrammer.AsyncEnumerables
{
    public sealed class AsyncEnumeratorContext
    {
        public byte[] Buffer { get; init; }

        public int Offset { get; set; }

        public int Length { get; set; }

        public IPEndPoint EndPoint { get; set; }

        public Exception Error { get; internal set; }
    }
}
