using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer.Dataflow
{
    public sealed class DatagramBlockOptions
    {
        public TaskScheduler TaskScheduler { get; set; }

        public CancellationToken? CancellationToken { get; set; }

        public int? SendingBufferCapacity { get; set; }

        public int? ReceivingBufferCapacity { get; set; }
    }
}
