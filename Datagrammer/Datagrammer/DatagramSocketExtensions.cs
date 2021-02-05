using Datagrammer.AsyncEnumerables;
using Datagrammer.Channels;
using Datagrammer.Dataflow;
using Datagrammer.Observables;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer
{
    public static class DatagramSocketExtensions
    {
        public static IAsyncEnumerable<AsyncEnumeratorContext> ToOutputEnumerable(this IDatagramSocket socket)
        {
            return new OutputAsyncEnumerable(socket);
        }

        public static IAsyncEnumerable<AsyncEnumeratorContext> ToInputEnumerable(this IDatagramSocket socket)
        {
            return new InputAsyncEnumerable(socket);
        }

        public static Channel<Try<Datagram>> ToChannel(this IDatagramSocket socket, Action<DatagramChannelOptions> configuration = null)
        {
            var options = new DatagramChannelOptions();

            configuration?.Invoke(options);

            var channel = new DatagramChannel(socket, options);

            channel.Start();

            return channel;
        }

        public static IPropagatorBlock<Try<Datagram>, Try<Datagram>> ToDataflowBlock(this IDatagramSocket socket, Action<DatagramBlockOptions> configuration = null)
        {
            var options = new DatagramBlockOptions();

            configuration?.Invoke(options);

            var block = new DatagramBlock(socket, options);

            block.Start();

            return block;
        }

        public static ISubject<Try<Datagram>> ToObservable(this IDatagramSocket socket, Action<ObservableOptions> configuration = null)
        {
            var options = new ObservableOptions();

            configuration?.Invoke(options);

            var block = socket.ToDataflowBlock(opt =>
            {
                opt.TaskScheduler = options.TaskScheduler;
            });

            return Subject.Create<Try<Datagram>>(block.AsObserver(), block.AsObservable());
        }
    }
}
