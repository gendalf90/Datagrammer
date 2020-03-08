using System;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Channels
{
    public static class ChannelExtensions
    {
        public static Channel<Datagram> AsChannel(this IPropagatorBlock<Datagram, Datagram> propagatorBlock, Action<ChannelOptions> configuration = null)
        {
            var options = new ChannelOptions();

            configuration?.Invoke(options);

            return new ChannelAdapter(propagatorBlock, options);
        }
    }
}
