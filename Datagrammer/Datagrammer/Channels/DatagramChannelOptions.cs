using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Datagrammer.Channels
{
    public sealed class DatagramChannelOptions
    {
        public Socket Socket { get; set; }

        public EndPoint ListeningPoint { get; set; }

        public TaskScheduler TaskScheduler { get; set; }

        public int? SendingBufferCapacity { get; set; }

        public BoundedChannelFullMode? SendingFullMode { get; set; }

        public int? ReceivingBufferCapacity { get; set; }

        public BoundedChannelFullMode? ReceivingFullMode { get; set; }

        public bool? AllowSynchronousContinuations { get; set; }

        public bool? SingleReader { get; set; }

        public bool? SingleWriter { get; set; }
    }
}
