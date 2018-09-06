using System.Net;

namespace Datagrammer
{
    internal class MessageDto
    {
        public IPEndPoint EndPoint { get; set; }

        public byte[] Bytes { get; set; }
    }
}
