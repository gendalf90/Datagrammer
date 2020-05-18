using Datagrammer.SocketEventArgs;
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
        private const int NotInitializedState = 0;
        private const int InitializedState = 1;

        private readonly Channel<Datagram> sendingChannel;
        private readonly Channel<Datagram> receivingChannel;
        private readonly Socket socket;
        private readonly EndPoint listeningPoint;
        private readonly TaskScheduler taskScheduler;
        private readonly CancellationToken cancellationToken;
        private readonly bool disposeSocketAfterCompletion;
        private readonly SendingSocketAsyncEventArgs sendingSocketEventArgs;
        private readonly ReceivingSocketAsyncEventArgs receivingSocketEventArgs;

        private int state = NotInitializedState;

        public DatagramChannel() : this(new DatagramChannelOptions())
        {
        }

        public DatagramChannel(DatagramChannelOptions options)
        {
            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            socket = options.Socket ?? throw new ArgumentNullException(nameof(options.Socket));
            listeningPoint = options.ListeningPoint ?? throw new ArgumentNullException(nameof(options.ListeningPoint));
            taskScheduler = options.TaskScheduler ?? throw new ArgumentNullException(nameof(options.TaskScheduler));

            cancellationToken = options.CancellationToken;
            disposeSocketAfterCompletion = options.DisposeSocketAfterCompletion;

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

            sendingSocketEventArgs = new SendingSocketAsyncEventArgs();
            receivingSocketEventArgs = new ReceivingSocketAsyncEventArgs(socket.AddressFamily);

            Writer = sendingChannel.Writer;
            Reader = receivingChannel.Reader;
        }

        public void Start()
        {
            if (!TryStartInitialization())
            {
                return;
            }

            try
            {
                StartClientListening();
                StartProcessing();
            }
            catch (Exception e)
            {
                FaultAll(e);
            }
        }

        private bool TryStartInitialization()
        {
            var previousState = Interlocked.CompareExchange(ref state, InitializedState, NotInitializedState);
            return previousState == NotInitializedState;
        }

        private void StartClientListening()
        {
            if (!socket.IsBound)
            {
                socket.Bind(listeningPoint);
            }
        }

        private void StartProcessing()
        {
            StartAsyncActions(
                DisposeSocketIfNeededAsync,
                CancelIfNeededAsync,
                CompleteReceivingAsync,
                StartSendingAsync,
                StartReceivingAsync);
        }

        private async Task StartReceivingAsync()
        {
            try
            {
                while (await receivingChannel.Writer.WaitToWriteAsync(cancellationToken))
                {
                    var datagram = await ReceiveAsync();

                    if(datagram == Datagram.Empty)
                    {
                        continue;
                    }

                    if(!receivingChannel.Writer.TryWrite(datagram))
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                FaultAll(e);
            }
        }

        private async ValueTask<Datagram> ReceiveAsync()
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

                return receivingSocketEventArgs.GetDatagram();
            }
            catch (SocketException e)
            {
                SocketErrorHandler?.Invoke(this, new SocketErrorEventArgs(e));

                return Datagram.Empty;
            }
            finally
            {
                receivingSocketEventArgs.Reset();
            }
        }

        private async Task StartSendingAsync()
        {
            try
            {
                while(await sendingChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    while(sendingChannel.Reader.TryRead(out var datagram))
                    {
                        await SendAsync(datagram);
                    }
                }
            }
            catch (Exception e)
            {
                FaultAll(e);
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
                SocketErrorHandler?.Invoke(this, new SocketErrorEventArgs(e));
            }
            finally
            {
                sendingSocketEventArgs.Reset();
            }
        }

        private void StartAsyncActions(params Func<Task>[] asyncActions)
        {
            foreach (var action in asyncActions)
            {
                Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, taskScheduler);
            }
        }

        private async Task DisposeSocketIfNeededAsync()
        {
            if (!disposeSocketAfterCompletion)
            {
                return;
            }

            using (socket)
            {
                await Completion;
            }
        }

        private async Task CancelIfNeededAsync()
        {
            await using (cancellationToken.Register(() => FaultAll(new OperationCanceledException(cancellationToken))))
            {
                await Completion;
            }
        }

        private Task Completion => Task.WhenAll(sendingChannel.Reader.Completion, receivingChannel.Reader.Completion);

        private async Task CompleteReceivingAsync()
        {
            try
            {
                await sendingChannel.Reader.Completion;

                receivingChannel.Writer.TryComplete();
            }
            catch(Exception e)
            {
                receivingChannel.Writer.TryComplete(e);
            }
        }

        private void FaultAll(Exception e)
        {
            sendingChannel.Writer.TryComplete(e);
            receivingChannel.Writer.TryComplete(e);
        }

        public event EventHandler<SocketErrorEventArgs> SocketErrorHandler;
    }
}
