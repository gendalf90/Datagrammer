using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer.Dataflow
{
    public sealed class DatagramBlockOptions
    {
        public TaskScheduler TaskScheduler { get; set; } = TaskScheduler.Default;

        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public int SendingBufferCapacity { get; set; } = 1;

        public int ReceivingBufferCapacity { get; set; } = 1;

        public bool CompleteChannel { get; set; } = false;
    }
}
