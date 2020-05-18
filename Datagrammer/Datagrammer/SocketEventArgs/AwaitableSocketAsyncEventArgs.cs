using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Datagrammer.SocketEventArgs
{
    internal class AwaitableSocketAsyncEventArgs : SocketAsyncEventArgs, IValueTaskSource
    {
        protected const int MaxUDPSize = 0x10000;

        private ManualResetValueTaskSourceCore<SocketError> awaiterSource = new ManualResetValueTaskSourceCore<SocketError>();

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

        public virtual void Reset()
        {
            awaiterSource.Reset();
        }
    }
}
