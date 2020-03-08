using System;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Timeout
{
    public static class TimeoutExtensions
    {
        public static ITargetBlock<T> WithTimeout<T>(this ITargetBlock<T> target, Action<TimeoutOptions> configuration = null)
        {
            var options = new TimeoutOptions();

            configuration?.Invoke(options);

            return new TimeoutDecorator<T>(target, options);
        }
    }
}
