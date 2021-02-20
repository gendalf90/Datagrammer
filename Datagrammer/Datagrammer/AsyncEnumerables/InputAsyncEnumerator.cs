using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer.AsyncEnumerables
{
    internal sealed class InputAsyncEnumerator : IAsyncEnumerator<AsyncEnumeratorContext>
    {
        private readonly Socket socket;
        private readonly CancellationToken cancellationToken;
        private readonly AsyncEnumeratorContext context;
        private readonly ValueTaskSource<SocketError> taskSource;
        private readonly SocketAsyncEventArgs socketEventArgs;

        private bool hasResult;

        public InputAsyncEnumerator(Socket socket, CancellationToken cancellationToken)
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
            if (!hasResult)
            {
                cancellationToken.ThrowIfCancellationRequested();

                hasResult = true;

                return true;
            }

            try
            {
                var useAsyncWaiting = false;

                cancellationToken.ThrowIfCancellationRequested();

                SetContext();

                useAsyncWaiting = SendAsync();

                if (useAsyncWaiting)
                {
                    await taskSource.Task;
                }
                else
                {
                    socketEventArgs.ThrowIfNotSuccess();
                }

                SetSuccess();
            }
            catch (SocketException e)
            {
                SetError(e);
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

        private bool SendAsync()
        {
            return socketEventArgs.RemoteEndPoint == null
                ? socket.SendAsync(socketEventArgs)
                : socket.SendToAsync(socketEventArgs);
        }

        private void SetSuccess()
        {
            context.Error = null;
        }

        private void SetError(Exception e)
        {
            context.Error = e;
        }

        private void SetContext()
        {
            socketEventArgs.RemoteEndPoint = context.EndPoint;
            socketEventArgs.SetBuffer(context.Offset, context.Length);
        }

        public async ValueTask DisposeAsync()
        {
            socketEventArgs.Dispose();

            await taskSource.DisposeAsync();

            hasResult = false;
        }
    }
}
