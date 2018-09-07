using System.Net;

namespace Datagrammer
{
    public sealed class Datagram
    {
        public IPEndPoint EndPoint { get; set; }

        public byte[] Bytes { get; set; }
    }
}
