using Datagrammer.Dataflow.Middleware;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Dataflow
{
    public static class DataflowExtensions
    {
        public static IPropagatorBlock<TResultInput, TMiddlewareOutput> UseAfter<TResultInput, TMiddlewareInput, TMiddlewareOutput>(this IPropagatorBlock<TResultInput, TMiddlewareInput> propagator, Func<TMiddlewareInput, Func<TMiddlewareOutput, Task>, Task> middleware, Action<MiddlewareOptions> configuration = null)
        {
            return DataflowBlock.Encapsulate(propagator, propagator.UseAfter<TMiddlewareInput, TMiddlewareOutput>(middleware, configuration));
        }

        public static IPropagatorBlock<TMiddlewareInput, TResultOutput> UseBefore<TMiddlewareInput, TMiddlewareOutput, TResultOutput>(this IPropagatorBlock<TMiddlewareOutput, TResultOutput> propagator, Func<TMiddlewareInput, Func<TMiddlewareOutput, Task>, Task> middleware, Action<MiddlewareOptions> configuration = null)
        {
            return DataflowBlock.Encapsulate(propagator.UseBefore<TMiddlewareInput, TMiddlewareOutput>(middleware, configuration), propagator);
        }

        public static ISourceBlock<TOutput> UseAfter<TInput, TOutput>(this ISourceBlock<TInput> source, Func<TInput, Func<TOutput, Task>, Task> middleware, Action<MiddlewareOptions> configuration = null)
        {
            var options = new MiddlewareOptions();

            configuration?.Invoke(options);

            var actionMiddleware = new ActionMiddlewareBlock<TInput, TOutput>(middleware, options);

            source.LinkTo(actionMiddleware, new DataflowLinkOptions { PropagateCompletion = true });

            return actionMiddleware;
        }

        public static ITargetBlock<TInput> UseBefore<TInput, TOutput>(this ITargetBlock<TOutput> target, Func<TInput, Func<TOutput, Task>, Task> middleware, Action<MiddlewareOptions> configuration = null)
        {
            var options = new MiddlewareOptions();

            configuration?.Invoke(options);

            var actionMiddleware = new ActionMiddlewareBlock<TInput, TOutput>(middleware, options);

            actionMiddleware.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true });

            return actionMiddleware;
        }

        public static IPropagatorBlock<Datagram, Datagram> ToDataflowBlock(this Channel<Datagram> channel, Action<DatagramBlockOptions> configuration = null)
        {
            var options = new DatagramBlockOptions();

            configuration?.Invoke(options);

            return new DatagramBlock(channel, options);
        }
    }
}
