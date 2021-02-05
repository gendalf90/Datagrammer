using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Datagrammer
{
    internal sealed class ValueTaskSource<T> : IValueTaskSource<T>
    {
        private ManualResetValueTaskSourceCore<T> core = new ManualResetValueTaskSourceCore<T>();

        public T GetResult(short token)
        {
            return core.GetResult(token);
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return core.GetStatus(token);
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            core.OnCompleted(continuation, state, token, flags);
        }

        public ValueTask<T> Task => new ValueTask<T>(this, core.Version);

        public void SetResult(T result)
        {
            core.SetResult(result);
        }

        public void SetException(Exception e)
        {
            core.SetException(e);
        }

        public void Reset()
        {
            core.Reset();
        }
    }
}
