using System;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Channels
{
    public static class ChannelExtensions
    {
        public static Channel<T> AsChannel<T>(this IPropagatorBlock<T, T> propagatorBlock, Action<ChannelOptions> configuration = null)
        {
            var options = new ChannelOptions();

            configuration?.Invoke(options);

            return new ChannelAdapter<T>(propagatorBlock, options);
        }
    }
}
