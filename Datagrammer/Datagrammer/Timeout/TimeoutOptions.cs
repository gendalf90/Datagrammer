using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer.Timeout
{
    public sealed class TimeoutOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public TaskScheduler TaskScheduler { get; set; } = TaskScheduler.Default;

        public int InputBufferCapacity { get; set; } = 1;

        public int OutputBufferCapacity { get; set; } = 1;
    }
}
