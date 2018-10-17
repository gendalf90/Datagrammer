using System;
using System.Net;

namespace Datagrammer
{
    public sealed class DatagramOptions
    {
        public IPEndPoint ListeningPoint { get; set; } = new IPEndPoint(IPAddress.Any, 5000);
    }
}
