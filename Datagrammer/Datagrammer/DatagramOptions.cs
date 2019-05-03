using System.Net;

namespace Datagrammer
{
    public sealed class DatagramOptions
    {
        public IPEndPoint ListeningPoint { get; set; } = new IPEndPoint(IPAddress.Any, 5000);

        public int SendingBufferCapacity { get; set; } = 1;

        public int SendingParallelismDegree { get; set; } = 1;

        public int ReceivingBufferCapacity { get; set; } = 1;
    }
}
