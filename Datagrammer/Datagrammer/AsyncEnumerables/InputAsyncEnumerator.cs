using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer.AsyncEnumerables
{
    internal sealed class InputAsyncEnumerator : IAsyncEnumerator<AsyncEnumeratorContext>
    {
        private readonly IDatagramSocket socket;
        private readonly CancellationToken cancellationToken;
        private readonly CancellationTokenRegistration cancellationTokenRegistration;
        private readonly Memory<byte> defaultBuffer;
        private readonly AsyncEnumeratorContext data = new AsyncEnumeratorContext();
        private readonly ValueTaskSource<SocketError> taskSource = new ValueTaskSource<SocketError>();
        private readonly SocketAsyncEventArgs socketEventArgs = new SocketAsyncEventArgs();
        private readonly object locker = new object();

        private bool hasResult;

        public InputAsyncEnumerator(IDatagramSocket socket, CancellationToken cancellationToken)
        {
            this.socket = socket;
            this.cancellationToken = cancellationToken;

            defaultBuffer = socketEventArgs.InitializeBuffer();
            cancellationTokenRegistration = cancellationToken.Register(Cancel);
            socketEventArgs.Completed += HandleResult;
        }

        public AsyncEnumeratorContext Current => hasResult ? data : throw new ArgumentOutOfRangeException(nameof(Current));

        public async ValueTask<bool> MoveNextAsync()
        {
            if (!hasResult)
            {
                InitializeContext();

                hasResult = true;

                return true;
            }

            try
            {
                var useAsyncWaiting = false;

                lock (locker)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    SetContext();

                    useAsyncWaiting = socket.SendToAsync(socketEventArgs);
                }

                if (useAsyncWaiting)
                {
                    await taskSource.Task;
                }
                else
                {
                    socketEventArgs.ThrowIfNotSuccess();
                }

                InitializeContext();
            }
            catch (SocketException e)
            {
                InitializeContext(e);
            }
            finally
            {
                taskSource.Reset();
            }

            hasResult = true;

            return true;
        }

        private void HandleResult(object obj, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                taskSource.SetResult(args.SocketError);
            }
            else if (args.SocketError == SocketError.OperationAborted && cancellationToken.IsCancellationRequested)
            {
                taskSource.SetException(new OperationCanceledException(cancellationToken));
            }
            else
            {
                taskSource.SetException(new SocketException((int)args.SocketError));
            }
        }

        private void InitializeContext(Exception e = null)
        {
            data.EndPoint = null;
            data.Buffer = defaultBuffer;
            data.Error = e;
        }

        private void SetContext()
        {
            if (data.EndPoint == null)
            {
                throw new SocketException((int)SocketError.AddressNotAvailable);
            }

            socketEventArgs.RemoteEndPoint = data.EndPoint;

            if (data.Buffer.Length > DatagramBuffer.MaxSize)
            {
                throw new SocketException((int)SocketError.MessageSize);
            }

            socketEventArgs.SetBuffer(data.Buffer);
        }

        private void Cancel()
        {
            lock (locker)
            {
                socketEventArgs.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await cancellationTokenRegistration.DisposeAsync();
            socketEventArgs.Dispose();

            hasResult = false;
        }
    }
}
