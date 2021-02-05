using System.Threading.Channels;

namespace Datagrammer.Channels
{
    public sealed class DatagramChannelOptions
    {
        public int? SendingBufferCapacity { get; set; }

        public BoundedChannelFullMode? SendingFullMode { get; set; }

        public int? ReceivingBufferCapacity { get; set; }

        public BoundedChannelFullMode? ReceivingFullMode { get; set; }

        public bool? AllowSynchronousContinuations { get; set; }

        public bool? SingleReader { get; set; }

        public bool? SingleWriter { get; set; }
    }
}
