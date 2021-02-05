using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer.AsyncEnumerables
{
    internal sealed class OutputAsyncEnumerator : IAsyncEnumerator<AsyncEnumeratorContext>
    {
        private static readonly IPAddress defaultReceiveAddress = IPAddress.Any;
        private static readonly int defaultReceivePort = IPEndPoint.MinPort;

        private readonly IDatagramSocket socket;
        private readonly CancellationToken cancellationToken;
        private readonly CancellationTokenRegistration cancellationTokenRegistration;
        private readonly IPEndPoint defaultEndPoint;
        private readonly AsyncEnumeratorContext data = new AsyncEnumeratorContext();
        private readonly ValueTaskSource<SocketError> taskSource = new ValueTaskSource<SocketError>();
        private readonly SocketAsyncEventArgs socketEventArgs = new SocketAsyncEventArgs();
        private readonly object locker = new object();

        private bool hasResult;

        public OutputAsyncEnumerator(IDatagramSocket socket, CancellationToken cancellationToken)
        {
            this.socket = socket;
            this.cancellationToken = cancellationToken;

            socketEventArgs.InitializeBuffer();
            defaultEndPoint = new IPEndPoint(defaultReceiveAddress, defaultReceivePort);
            cancellationTokenRegistration = cancellationToken.Register(Cancel);
            socketEventArgs.Completed += HandleResult;
        }

        public AsyncEnumeratorContext Current => hasResult ? data : throw new ArgumentOutOfRangeException(nameof(Current));

        public async ValueTask<bool> MoveNextAsync()
        {
            try
            {
                var useAsyncWaiting = false;

                lock (locker)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    InitializeRemoteEndPoint();

                    useAsyncWaiting = socket.ReceiveFromAsync(socketEventArgs);
                }

                if (useAsyncWaiting)
                {
                    await taskSource.Task;
                }
                else
                {
                    socketEventArgs.ThrowIfNotSuccess();
                }

                FillContext();
            }
            catch (SocketException e)
            {
                FillContext(e);
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

        private void Cancel()
        {
            lock (locker)
            {
                socketEventArgs.Dispose();
            }
        }

        private void InitializeRemoteEndPoint()
        {
            defaultEndPoint.Address = defaultReceiveAddress;
            defaultEndPoint.Port = defaultReceivePort;

            socketEventArgs.RemoteEndPoint = defaultEndPoint;
        }

        private void FillContext(Exception e = null)
        {
            data.EndPoint = (IPEndPoint)socketEventArgs.RemoteEndPoint;
            data.Buffer = socketEventArgs.MemoryBuffer.Slice(0, socketEventArgs.BytesTransferred);
            data.Error = e;
        }

        public async ValueTask DisposeAsync()
        {
            await cancellationTokenRegistration.DisposeAsync();
            socketEventArgs.Dispose();

            hasResult = false;
        }
    }
}
