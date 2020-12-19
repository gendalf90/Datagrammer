using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Datagrammer
{
    internal class AwaitableSocketAsyncEventArgs : SocketAsyncEventArgs, IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<SocketError> awaiterSource = new ManualResetValueTaskSourceCore<SocketError>();
        
        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            base.OnCompleted(e);

            SetAsyncResult(e.SocketError);
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

        public void Reset()
        {
            awaiterSource.Reset();
        }

        private void SetAsyncResult(SocketError result)
        {
            if (result == SocketError.Success)
            {
                awaiterSource.SetResult(result);
            }
            else
            {
                awaiterSource.SetException(new SocketException((int)result));
            }
        }
    }
}
