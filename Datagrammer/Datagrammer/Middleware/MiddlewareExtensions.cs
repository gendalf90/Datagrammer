using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Middleware
{
    public static class MiddlewareExtensions
    {
        public static IPropagatorBlock<TInput, TMiddlewareOutput> UseAfter<TInput, TMiddlewareInput, TMiddlewareOutput>(this IPropagatorBlock<TInput, TMiddlewareInput> propagator, Func<TMiddlewareInput, Func<TMiddlewareOutput, Task>, Task> middleware, Action<MiddlewareOptions> configuration = null)
        {
            return DataflowBlock.Encapsulate(propagator, propagator.UseAfter<TMiddlewareInput, TMiddlewareOutput>(middleware, configuration));
        }

        public static IPropagatorBlock<TMiddlewareInput, TOutput> UseBefore<TMiddlewareInput, TMiddlewareOutput, TOutput>(this IPropagatorBlock<TMiddlewareOutput, TOutput> propagator, Func<TMiddlewareInput, Func<TMiddlewareOutput, Task>, Task> middleware, Action<MiddlewareOptions> configuration = null)
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
    }
}
