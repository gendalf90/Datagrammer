using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Channels
{
    public static class ChannelExtensions
    {
        public static Channel<Datagram> AsChannel(this IPropagatorBlock<Datagram, Datagram> propagatorBlock)
        {
            return new ChannelAdapter(propagatorBlock);
        }
    }
}
