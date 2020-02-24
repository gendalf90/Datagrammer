using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Datagrammer
{
    internal class AwaitableSocketAsyncEventArgs : SocketAsyncEventArgs, IValueTaskSource
    {
        private const int MaxUDPSize = 0x10000;
        private const int HasResultState = 1;
        private const int HasNoResultState = 0;

        private ManualResetValueTaskSourceCore<SocketError> awaiterSource = new ManualResetValueTaskSourceCore<SocketError>();
        private int resultState = HasNoResultState;

        public AwaitableSocketAsyncEventArgs()
        {
            RemoteEndPoint = new IPEndPoint(IPAddress.None, IPEndPoint.MinPort);

            SetBuffer(new byte[MaxUDPSize], 0, MaxUDPSize);
        }

        public void SetAnyEndPoint(AddressFamily addressFamily)
        {
            var ipEndPoint = (IPEndPoint)RemoteEndPoint;

            if(addressFamily == AddressFamily.InterNetworkV6)
            {
                ipEndPoint.Address = IPAddress.IPv6Any;
            }
            else
            {
                ipEndPoint.Address = IPAddress.Any;
            }

            ipEndPoint.Port = IPEndPoint.MinPort;
        }

        public void SetDatagram(Datagram datagram)
        {
            var ipEndPoint = (IPEndPoint)RemoteEndPoint;

            if(!datagram.TryGetIPAddress(out var ipAddress))
            {
                throw new SocketException((int)SocketError.DestinationAddressRequired);
            }

            ipEndPoint.Address = ipAddress;
            ipEndPoint.Port = datagram.Port;

            if(!datagram.Buffer.TryCopyTo(MemoryBuffer))
            {
                throw new SocketException((int)SocketError.MessageSize);
            }

            SetBuffer(0, datagram.Buffer.Length);
        }

        public Datagram GetDatagram()
        {
            var ipEndPoint = (IPEndPoint)RemoteEndPoint;
            var buffer = MemoryBuffer
                .Slice(0, BytesTransferred)
                .ToArray();

            return new Datagram(buffer, ipEndPoint);
        }

        public void ThrowIfNotSuccess()
        {
            if(SocketError != SocketError.Success)
            {
                throw new SocketException((int)SocketError);
            }
        }

        private bool TryStartResultModifying()
        {
            var previousResultState = Interlocked.CompareExchange(ref resultState, HasResultState, HasNoResultState);
            return previousResultState == HasNoResultState;
        }

        private void OnCancel(CancellationToken token)
        {
            if(TryStartResultModifying())
            {
                awaiterSource.SetException(new OperationCanceledException(token));
            }
        }

        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            base.OnCompleted(e);

            if(!TryStartResultModifying())
            {
                return;
            }

            if (e.SocketError == SocketError.Success)
            {
                awaiterSource.SetResult(e.SocketError);
            }
            else
            {
                awaiterSource.SetException(new SocketException((int)e.SocketError));
            }
        }

        public async ValueTask WaitUntilCompletedAsync(CancellationToken token)
        {
            using (token.Register(() => OnCancel(token)))
            {
                await new ValueTask(this, awaiterSource.Version);
            }
        }

        public void GetResult(short token)
        {
            awaiterSource.GetResult(token);
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return awaiterSource.GetStatus(token);
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            awaiterSource.OnCompleted(continuation, state, token, flags);
        }

        public void Reset()
        {
            SetBuffer(0, MaxUDPSize);

            awaiterSource.Reset();

            Interlocked.Exchange(ref resultState, HasNoResultState);
        }
    }
}
