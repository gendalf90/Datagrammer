using System;
using System.Collections.Concurrent;

namespace Datagrammer
{
    internal class SocketEventPool : IDisposable
    {
        private readonly ConcurrentQueue<AwaitableSocketAsyncEventArgs> pool = new ConcurrentQueue<AwaitableSocketAsyncEventArgs>();

        public AwaitableSocketAsyncEventArgs GetOrCreate()
        {
            if (pool.TryDequeue(out var socketEvent))
            {
                return socketEvent;
            }

            return new AwaitableSocketAsyncEventArgs();
        }

        public void Release(AwaitableSocketAsyncEventArgs socketEvent)
        {
            pool.Enqueue(socketEvent);
        }

        public void Dispose()
        {
            while(pool.TryDequeue(out var socketEvent))
            {
                socketEvent.Dispose();
            }
        }
    }
}
