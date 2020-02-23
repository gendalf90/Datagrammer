using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Datagrammer
{
    internal class AwaitableSocketAsyncEventArgs : SocketAsyncEventArgs, IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<SocketError> awaiterSource = new ManualResetValueTaskSourceCore<SocketError>();

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

        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            base.OnCompleted(e);

            if (e.SocketError == SocketError.Success)
            {
                awaiterSource.SetResult(e.SocketError);
            }
            else
            {
                awaiterSource.SetException(new SocketException((int)e.SocketError));
            }
        }

        public ValueTaskAwaiter GetAwaiter()
        {
            return new ValueTask(this, awaiterSource.Version).GetAwaiter();
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
        }
    }
}
