using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Datagrammer
{
    public sealed class DatagramChannelOptions
    {
        public Socket Socket { get; set; } = new Socket(SocketType.Dgram, ProtocolType.Udp);

        public EndPoint ListeningPoint { get; set; } = new IPEndPoint(IPAddress.Any, 50000);

        public TaskScheduler TaskScheduler { get; set; } = TaskScheduler.Default;

        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public int SendingBufferCapacity { get; set; } = 1;

        public BoundedChannelFullMode SendingFullMode { get; set; } = BoundedChannelFullMode.Wait;

        public int ReceivingBufferCapacity { get; set; } = 1;

        public BoundedChannelFullMode ReceivingFullMode { get; set; } = BoundedChannelFullMode.Wait;

        public bool DisposeSocket { get; set; } = true;

        public Func<Exception, Task> ErrorHandler { get; set; }
    }
}
