using System;
using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer.Timeout
{
    public sealed class TimeoutOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public TaskScheduler TaskScheduler { get; set; } = TaskScheduler.Default;

        public TimeSpan Timeout { get; set; } = System.Threading.Timeout.InfiniteTimeSpan;

        public int BufferCapacity { get; set; } = 1;
    }
}
