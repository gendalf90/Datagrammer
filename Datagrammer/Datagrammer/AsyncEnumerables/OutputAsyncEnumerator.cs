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
        private readonly Socket socket;
        private readonly CancellationToken cancellationToken;
        private readonly AsyncEnumeratorContext context;
        private readonly ValueTaskSource<SocketError> taskSource;
        private readonly SocketAsyncEventArgs socketEventArgs;

        private bool hasResult;

        public OutputAsyncEnumerator(Socket socket, CancellationToken cancellationToken)
        {
            this.socket = socket;
            this.cancellationToken = cancellationToken;

            socketEventArgs = new SocketAsyncEventArgs();
            socketEventArgs.SetBuffer();
            socketEventArgs.Completed += HandleResult;
            context = new AsyncEnumeratorContext { Buffer = socketEventArgs.Buffer };
            taskSource = new ValueTaskSource<SocketError>(cancellationToken);
        }

        public AsyncEnumeratorContext Current => hasResult ? context : throw new ArgumentOutOfRangeException(nameof(Current));

        public async ValueTask<bool> MoveNextAsync()
        {
            try
            {
                var useAsyncWaiting = false;

                cancellationToken.ThrowIfCancellationRequested();

                InitializeRemoteEndPoint();

                useAsyncWaiting = socket.ReceiveFromAsync(socketEventArgs);

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
                taskSource.TrySetResult(args.SocketError);
            }
            else
            {
                taskSource.TrySetException(new SocketException((int)args.SocketError));
            }
        }

        private void InitializeRemoteEndPoint()
        {
            var receiveAddress = socket.AddressFamily == AddressFamily.InterNetwork
                ? IPAddress.Any
                : IPAddress.IPv6Any;
            var receivePort = IPEndPoint.MinPort;
            var receiveEndPoint = socketEventArgs.RemoteEndPoint as IPEndPoint;

            if (receiveEndPoint == null)
            {
                socketEventArgs.RemoteEndPoint = new IPEndPoint(receiveAddress, receivePort);
            }
            else
            {
                receiveEndPoint.Address = receiveAddress;
                receiveEndPoint.Port = receivePort;
            }
        }

        private void FillContext(Exception e = null)
        {
            context.EndPoint = (IPEndPoint)socketEventArgs.RemoteEndPoint;
            context.Offset = 0;
            context.Length = socketEventArgs.BytesTransferred;
            context.Error = e;
        }

        public async ValueTask DisposeAsync()
        {
            socketEventArgs.Dispose();

            await taskSource.DisposeAsync();

            hasResult = false;
        }
    }
}
