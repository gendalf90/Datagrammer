using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer
{
    public sealed class DatagramOptions
    {
        public Socket Socket { get; set; } = new Socket(SocketType.Dgram, ProtocolType.Udp);

        public EndPoint ListeningPoint { get; set; } = new IPEndPoint(IPAddress.Any, 5000);

        public TaskScheduler TaskScheduler { get; set; } = TaskScheduler.Default;

        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public int SendingBufferCapacity { get; set; } = 1;

        public int SendingParallelismDegree { get; set; } = 1;

        public int ReceivingBufferCapacity { get; set; } = 1;

        public int ReceivingParallelismDegree { get; set; } = 1;

        public bool DisposeSocketAfterCompletion { get; set; } = true;

        public Func<SocketException, Task> SocketErrorHandler { get; set; }
    }
}
