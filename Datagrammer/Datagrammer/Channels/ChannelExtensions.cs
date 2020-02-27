using System.Threading;
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

        public static Channel<Datagram> AsChannel(this IPropagatorBlock<Datagram, Datagram> propagatorBlock, CancellationToken cancellationToken)
        {
            return new ChannelAdapter(propagatorBlock, new ChannelAdapterOptions
            {
                CancellationToken = cancellationToken
            });
        }
    }
}
