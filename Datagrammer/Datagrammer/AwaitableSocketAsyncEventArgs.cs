using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
        }

        public void InitializeForReceiving()
        {
            SetBuffer(new byte[MaxUDPSize], 0, MaxUDPSize);
        }

        public Datagram GetDatagram()
        {
            var ipEndPoint = (IPEndPoint)RemoteEndPoint;
            var address = ipEndPoint.Address.GetAddressBytes();
            var buffer = MemoryBuffer
                .Slice(0, BytesTransferred)
                .ToArray();

            return new Datagram(buffer, address, ipEndPoint.Port);
        }

        public void SetDatagram(Datagram datagram)
        {
            var ipEndPoint = (IPEndPoint)RemoteEndPoint;

            try
            {
                ipEndPoint.Address = datagram.GetAddress();
                ipEndPoint.Port = datagram.Port;
            }
            catch
            {
                throw new SocketException((int)SocketError.AddressNotAvailable);
            }

            if (datagram.Buffer.Length > MaxUDPSize)
            {
                throw new SocketException((int)SocketError.MessageSize);
            }

            SetBuffer(MemoryMarshal.AsMemory(datagram.Buffer));
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

        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            base.OnCompleted(e);

            if (!TryStartResultModifying())
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

        public ValueTask WaitUntilCompletedAsync()
        {
            return new ValueTask(this, awaiterSource.Version);
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

        public void Close()
        {
            Dispose();

            if (TryStartResultModifying())
            {
                awaiterSource.SetException(new SocketException((int)SocketError.OperationAborted));
            }
        }

        public void Reset()
        {
            awaiterSource.Reset();

            Interlocked.Exchange(ref resultState, HasNoResultState);
        }
    }
}
