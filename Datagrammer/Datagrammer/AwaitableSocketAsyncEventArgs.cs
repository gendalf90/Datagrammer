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
        private const int HasResultState = 1;
        private const int HasNoResultState = 0;

        private ManualResetValueTaskSourceCore<SocketError> awaiterSource = new ManualResetValueTaskSourceCore<SocketError>();

        private int resultState = HasNoResultState;

        public AwaitableSocketAsyncEventArgs()
        {
            RemoteEndPoint = new IPEndPoint(IPAddress.None, 0);
        }

        public void SetDatagram(Datagram datagram)
        {
            var ipEndPoint = (IPEndPoint)RemoteEndPoint;

            ipEndPoint.Address = datagram.GetIPAddress();
            ipEndPoint.Port = datagram.Port;

            SetBuffer(MemoryMarshal.AsMemory(datagram.Buffer));
        }

        public Datagram GetDatagram()
        {
            var ipEndPoint = (IPEndPoint)RemoteEndPoint;

            return new Datagram(Buffer, ipEndPoint);
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
            awaiterSource.Reset();

            Interlocked.Exchange(ref resultState, HasNoResultState);
        }
    }
}
