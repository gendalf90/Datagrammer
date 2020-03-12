using System;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Timeout
{
    public static class TimeoutExtensions
    {
        public static IPropagatorBlock<TInput, TOutput> WithTimeout<TInput, TOutput>(this IPropagatorBlock<TInput, TOutput> target, TimeSpan timeout, Action<TimeoutOptions> configuration = null)
        {
            return DataflowBlock.Encapsulate(target.WithTimeout<TInput>(timeout, configuration), target);
        }

        public static ITargetBlock<T> WithTimeout<T>(this ITargetBlock<T> target, TimeSpan timeout, Action<TimeoutOptions> configuration = null)
        {
            var options = new TimeoutOptions();

            configuration?.Invoke(options);

            var timeoutBuffer = new TimeoutBufferBlock<T>(timeout, options);

            timeoutBuffer.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true });

            return timeoutBuffer;
        }
    }
}
