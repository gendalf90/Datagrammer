using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Datagrammer
{
    public sealed class DatagramChannel : Channel<Datagram>
    {
        private readonly Channel<Datagram> sendingChannel;
        private readonly Channel<Datagram> receivingChannel;
        private readonly Socket socket;
        private readonly EndPoint listeningPoint;
        private readonly TaskScheduler taskScheduler;
        private readonly CancellationToken cancellationToken;
        private readonly bool disposeSocketAfterCompletion;
        private readonly AwaitableSocketAsyncEventArgs sendingSocketEventArgs;
        private readonly AwaitableSocketAsyncEventArgs receivingSocketEventArgs;
        private readonly Func<SocketException, Task> errorHandler;

        private DatagramChannel(DatagramChannelOptions options)
        {
            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            socket = options.Socket ?? throw new ArgumentNullException(nameof(options.Socket));
            listeningPoint = options.ListeningPoint ?? throw new ArgumentNullException(nameof(options.ListeningPoint));
            taskScheduler = options.TaskScheduler ?? throw new ArgumentNullException(nameof(options.TaskScheduler));

            cancellationToken = options.CancellationToken;
            disposeSocketAfterCompletion = options.DisposeSocket;
            errorHandler = options.ErrorHandler;

            sendingChannel = Channel.CreateBounded<Datagram>(new BoundedChannelOptions(options.SendingBufferCapacity)
            {
                SingleReader = true,
                FullMode = options.SendingFullMode,
                AllowSynchronousContinuations = true
            });

            receivingChannel = Channel.CreateBounded<Datagram>(new BoundedChannelOptions(options.ReceivingBufferCapacity)
            {
                SingleWriter = true,
                FullMode = options.ReceivingFullMode,
                AllowSynchronousContinuations = true
            });

            sendingSocketEventArgs = new AwaitableSocketAsyncEventArgs();
            receivingSocketEventArgs = new AwaitableSocketAsyncEventArgs();

            Writer = sendingChannel.Writer;
            Reader = receivingChannel.Reader;
        }

        public static DatagramChannel Start(Action<DatagramChannelOptions> configuration = null)
        {
            var options = new DatagramChannelOptions();

            configuration?.Invoke(options);

            var channel = new DatagramChannel(options);

            channel.Start();
            
            return channel;
        }

        private void Start()
        {
            StartClientListening();
            StartAsyncActions(
                CancelIfNeededAsync,
                CloseSocketEventsAsync,
                StartSendingAsync,
                StartReceivingAsync);
        }

        private void StartClientListening()
        {
            receivingSocketEventArgs.InitializeForReceiving();

            if (!socket.IsBound)
            {
                socket.Bind(listeningPoint);
            }
        }

        private async Task StartReceivingAsync()
        {
            try
            {
                while (true)
                {
                    await ReceiveAsync();
                }
            }
            catch(Exception e)
            {
                Fault(e);
            }
            finally
            {
                await CompleteAsync();
            }
        }

        private async ValueTask ReceiveAsync()
        {
            try
            {
                if (socket.ReceiveFromAsync(receivingSocketEventArgs))
                {
                    await receivingSocketEventArgs.WaitUntilCompletedAsync();
                }
                else
                {
                    receivingSocketEventArgs.ThrowIfNotSuccess();
                }

                var datagram = receivingSocketEventArgs.GetDatagram();

                while (!receivingChannel.Writer.TryWrite(datagram))
                {
                    await receivingChannel.Writer.WaitToWriteAsync();
                }
            }
            catch (SocketException e)
            {
                await HandleErrorAsync(e);
            }
            finally
            {
                receivingSocketEventArgs.Reset();
            }
        }

        private async Task StartSendingAsync()
        {
            while (await sendingChannel.Reader.WaitToReadAsync())
            {
                while (sendingChannel.Reader.TryRead(out var datagram))
                {
                    try
                    {
                        await SendAsync(datagram);
                    }
                    catch (Exception e)
                    {
                        Fault(e);
                    }
                }
            }
        }

        private async ValueTask SendAsync(Datagram datagram)
        {
            try
            {
                sendingSocketEventArgs.SetDatagram(datagram);

                if (socket.SendToAsync(sendingSocketEventArgs))
                {
                    await sendingSocketEventArgs.WaitUntilCompletedAsync();
                }
                else
                {
                    sendingSocketEventArgs.ThrowIfNotSuccess();
                }
            }
            catch (SocketException e)
            {
                await HandleErrorAsync(e);
            }
            finally
            {
                sendingSocketEventArgs.Reset();
            }
        }

        private async ValueTask HandleErrorAsync(SocketException toHandleException)
        {
            if (errorHandler != null)
            {
                await errorHandler(toHandleException);
            }
        }

        private void StartAsyncActions(params Func<Task>[] asyncActions)
        {
            foreach (var action in asyncActions)
            {
                Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, taskScheduler);
            }
        }

        private async Task CloseSocketEventsAsync()
        {
            try
            {
                await sendingChannel.Reader.Completion;
            }
            finally
            {
                sendingSocketEventArgs.Close();
                receivingSocketEventArgs.Close();
            }
        }

        private async Task CancelIfNeededAsync()
        {
            await using (cancellationToken.Register(() => Fault(new OperationCanceledException(cancellationToken))))
            {
                await sendingChannel.Reader.Completion;
            }
        }

        private async ValueTask CompleteAsync()
        {
            try
            {
                await sendingChannel.Reader.Completion;

                Complete();
            }
            catch (Exception e)
            {
                Complete(e);
            }
        }

        private void Complete(Exception e = null)
        {
            if (disposeSocketAfterCompletion)
            {
                socket.Dispose();
            }

            receivingChannel.Writer.Complete(e);
        }

        private void Fault(Exception e)
        {
            sendingChannel.Writer.TryComplete(e);
        }
    }
}
