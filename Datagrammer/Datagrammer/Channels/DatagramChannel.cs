using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Datagrammer.Channels
{
    public sealed class DatagramChannel : Channel<Try<Datagram>>
    {
        private readonly TaskFactory taskFactory;
        private readonly Channel<Try<Datagram>> inputChannel;
        private readonly Channel<Try<Datagram>> outputChannel;
        private readonly Socket socket;
        private readonly EndPoint listeningPoint;
        private readonly AwaitableSocketAsyncEventArgs sendingSocketEventArgs;
        private readonly AwaitableSocketAsyncEventArgs receivingSocketEventArgs;
        private readonly bool needSocketBinding;
        private readonly TaskCompletionSource inputCompletionSource;
        private readonly TaskCompletionSource outputCompletionSource;

        private DatagramChannel(DatagramChannelOptions options)
        {
            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            taskFactory = new TaskFactory(options.TaskScheduler ?? TaskScheduler.Default);
            needSocketBinding = options.Socket == null || options.ListeningPoint != null;
            socket = options.Socket ?? new Socket(SocketType.Dgram, ProtocolType.Udp);
            listeningPoint = options.ListeningPoint ?? new IPEndPoint(IPAddress.Loopback, IPEndPoint.MinPort);
            
            inputChannel = Channel.CreateBounded<Try<Datagram>>(new BoundedChannelOptions(options.SendingBufferCapacity ?? 1)
            {
                SingleWriter = options.SingleWriter ?? false,
                SingleReader = true,
                FullMode = options.SendingFullMode ?? BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = options.AllowSynchronousContinuations ?? true
            });

            outputChannel = Channel.CreateBounded<Try<Datagram>>(new BoundedChannelOptions(options.ReceivingBufferCapacity ?? 1)
            {
                SingleWriter = false,
                SingleReader = options.SingleReader ?? false,
                FullMode = options.ReceivingFullMode ?? BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = options.AllowSynchronousContinuations ?? true
            });

            sendingSocketEventArgs = new AwaitableSocketAsyncEventArgs();
            receivingSocketEventArgs = new AwaitableSocketAsyncEventArgs();

            Writer = inputChannel.Writer;
            Reader = outputChannel.Reader;

            inputCompletionSource = new TaskCompletionSource();
            outputCompletionSource = new TaskCompletionSource();
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

            taskFactory.StartNew(StartSendingAsync);
            taskFactory.StartNew(StartReceivingAsync);
            taskFactory.StartNew(StartCompletionAsync);
        }

        private void StartClientListening()
        {
            receivingSocketEventArgs.InitializeForReceiving();

            if (needSocketBinding)
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
                outputCompletionSource.SetResult();

                Fault(e);
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

                await WriteToOutputAsync(new Try<Datagram>(datagram));
            }
            catch (SocketException e) when (e.SocketErrorCode != SocketError.OperationAborted)
            {
                await WriteToOutputAsync(new Try<Datagram>(e));
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
                while (await inputChannel.Reader.WaitToReadAsync())
                {
                    while (inputChannel.Reader.TryRead(out var datagram))
                    {
                        await ReadInputAsync(datagram);
                    }
                }
            }
            finally
            {
                inputCompletionSource.SetResult();
            }
        }

        private async ValueTask ReadInputAsync(Try<Datagram> datagram)
        {
            try
            {
                if (datagram.IsException())
                {
                    await WriteToOutputAsync(datagram);
                }
                else
                {
                    await SendAsync(datagram);
                }
            }
            catch (SocketException e) when (e.SocketErrorCode != SocketError.OperationAborted)
            {
                await WriteToOutputAsync(new Try<Datagram>(e));
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private ValueTask WriteToOutputAsync(Try<Datagram> datagram)
        {
            return outputChannel.Writer.WriteAsync(datagram);
        }

        private async ValueTask SendAsync(Try<Datagram> datagram)
        {
            try
            {
                sendingSocketEventArgs.SetDatagram(datagram.Value);

                if (socket.SendToAsync(sendingSocketEventArgs))
                {
                    await sendingSocketEventArgs.WaitUntilCompletedAsync();
                }
                else
                {
                    sendingSocketEventArgs.ThrowIfNotSuccess();
                }
            }
            finally
            {
                sendingSocketEventArgs.Reset();
            }
        }

        private async Task StartCompletionAsync()
        {
            using (sendingSocketEventArgs)
            using (receivingSocketEventArgs)
            using (socket)
            {
                await inputCompletionSource.Task;
            }

            await outputCompletionSource.Task;

            try
            {
                await inputChannel.Reader.Completion;

                outputChannel.Writer.TryComplete();
            }
            catch (Exception e)
            {
                outputChannel.Writer.TryComplete(e);
            }
        }

        private void Fault(Exception e)
        {
            inputChannel.Writer.TryComplete(e);
        }
    }
}
