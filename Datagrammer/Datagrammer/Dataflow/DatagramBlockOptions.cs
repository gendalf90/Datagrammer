using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer.Dataflow
{
    public sealed class DatagramBlockOptions
    {
        public Socket Socket { get; set; }

        public EndPoint ListeningPoint { get; set; }

        public TaskScheduler TaskScheduler { get; set; }

        public CancellationToken? CancellationToken { get; set; }

        public int? SendingBufferCapacity { get; set; }

        public int? ReceivingBufferCapacity { get; set; }
    }
}
