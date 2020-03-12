using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Middleware
{
    public static class MiddlewareExtensions
    {
        public static IPropagatorBlock<TInput, TOutput> UseAfter<TInput, TMiddlewareInput, TOutput>(this IPropagatorBlock<TInput, TMiddlewareInput> propagator, Func<TMiddlewareInput, Func<TOutput, Task>, Task> middleware, Action<MiddlewareOptions> configuration = null)
        {
            return DataflowBlock.Encapsulate(propagator, propagator.UseAfter<TMiddlewareInput, TOutput>(middleware, configuration));
        }

        public static IPropagatorBlock<TInput, TOutput> UseBefore<TInput, TMiddlewareOutput, TOutput>(this IPropagatorBlock<TMiddlewareOutput, TOutput> propagator, Func<TInput, Func<TMiddlewareOutput, Task>, Task> middleware, Action<MiddlewareOptions> configuration = null)
        {
            return DataflowBlock.Encapsulate(propagator.UseBefore<TInput, TMiddlewareOutput>(middleware, configuration), propagator);
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
