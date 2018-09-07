using System.Net;

namespace Datagrammer
{
    public sealed class DatagramOptions
    {
        public int ReceivingParallelismDegree { get; set; } = 1;

        public IPEndPoint ListeningPoint { get; set; } = new IPEndPoint(IPAddress.Any, 5000);
    }
}
