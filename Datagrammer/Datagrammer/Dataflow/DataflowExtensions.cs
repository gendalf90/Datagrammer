using System;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Dataflow
{
    public static class DataflowExtensions
    {
        public static IPropagatorBlock<Datagram, Datagram> ToDataflowBlock(this Channel<Datagram> channel, Action<DatagramBlockOptions> configuration = null)
        {
            return DatagramBlock.Start(channel, configuration);
        }
    }
}
