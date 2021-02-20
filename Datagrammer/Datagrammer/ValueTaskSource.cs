using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Datagrammer
{
    internal sealed class ValueTaskSource<T> : IAsyncDisposable
    {
        private ManualResetValueTaskSourceCore<T> core;
        private ValueTaskSourceInternal source;
        private CancellationToken token;
        private CancellationTokenRegistration cancellationTokenRegistration;
        private object syncObj;

        public ValueTaskSource(CancellationToken token = default)
        {
            this.token = token;

            core = new ManualResetValueTaskSourceCore<T>();
            source = new ValueTaskSourceInternal(this);
            cancellationTokenRegistration = token.Register(Cancel);
            syncObj = new object();
        }

        public ValueTask<T> Task
        {
            get
            {
                lock (syncObj)
                {
                    return token.IsCancellationRequested 
                        ? ValueTask.FromCanceled<T>(token) 
                        : new ValueTask<T>(source, core.Version);
                }
            }
        }

        public bool TrySetResult(T result)
        {
            lock (syncObj)
            {
                if (HasResult())
                {
                    return false;
                }

                core.SetResult(result);

                return true;
            }
        }

        public bool TrySetException(Exception e)
        {
            lock (syncObj)
            {
                if (HasResult())
                {
                    return false;
                }

                core.SetException(e);

                return true;
            }
        }

        public void Reset()
        {
            lock (syncObj)
            {
                core.Reset();
            }
        }

        private bool HasResult()
        {
            return core.GetStatus(core.Version) != ValueTaskSourceStatus.Pending;
        }

        private void Cancel()
        {
            lock (syncObj)
            {
                if (HasResult())
                {
                    return;
                }

                core.SetException(new OperationCanceledException(token));
            }
        }

        public async ValueTask DisposeAsync()
        {
            await cancellationTokenRegistration.DisposeAsync();
        }

        private class ValueTaskSourceInternal : IValueTaskSource<T>
        {
            private ValueTaskSource<T> source;

            public ValueTaskSourceInternal(ValueTaskSource<T> source)
            {
                this.source = source;
            }

            public T GetResult(short token)
            {
                return source.core.GetResult(token);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return source.core.GetStatus(token);
            }

            public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                source.core.OnCompleted(continuation, state, token, flags);
            }
        }
    }
}
