using Datagrammer.AsyncEnumerables;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Datagrammer.Channels
{
    internal sealed class DatagramChannel : Channel<Try<Datagram>>
    {
        private readonly Socket socket;
        private readonly Channel<Try<Datagram>> inputChannel;
        private readonly Channel<Try<Datagram>> outputChannel;
        private readonly TaskCompletionSource inputCompletionSource;
        private readonly TaskCompletionSource outputCompletionSource;
        private readonly CancellationTokenSource outputCancellationSource;

        public DatagramChannel(Socket socket, DatagramChannelOptions options)
        {
            if(socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.socket = socket;
            
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

            Writer = inputChannel.Writer;
            Reader = outputChannel.Reader;

            inputCompletionSource = new TaskCompletionSource();
            outputCompletionSource = new TaskCompletionSource();
            outputCancellationSource = new CancellationTokenSource();
        }

        public void Start()
        {
            var factory = new TaskFactory(TaskScheduler.Current);

            factory.StartNew(StartSendingAsync);
            factory.StartNew(StartReceivingAsync);
            factory.StartNew(StartCompletionAsync);
        }

        private async Task StartReceivingAsync()
        {
            try
            {
                await foreach (var context in socket.ToOutputEnumerable().WithCancellation(outputCancellationSource.Token))
                {
                    if (context.Error != null)
                    {
                        await WriteToOutputAsync(new Try<Datagram>(context.Error));
                    }
                    else
                    {
                        await ReceiveDatagramAsync(context);
                    }
                }
            }
            catch(OperationCanceledException)
            {
                outputCompletionSource.SetResult();
            }
            catch(Exception e)
            {
                outputCompletionSource.SetResult();

                inputChannel.Writer.TryComplete(e);
            }
        }

        private async ValueTask ReceiveDatagramAsync(AsyncEnumeratorContext context)
        {
            var datagram = GetDatagram(context);

            await WriteToOutputAsync(new Try<Datagram>(datagram));
        }

        private Datagram GetDatagram(AsyncEnumeratorContext context)
        {
            return new Datagram(
                context.Buffer.AsMemory(context.Offset, context.Length).ToArray(), 
                context.EndPoint.Address.GetAddressBytes(), 
                context.EndPoint.Port);
        }

        private async Task StartSendingAsync()
        {
            try
            {
                await foreach (var context in socket.ToInputEnumerable())
                {
                    if (context.Error != null)
                    {
                        await WriteToOutputAsync(new Try<Datagram>(context.Error));
                    }

                    await foreach (var datagram in inputChannel.Reader.ReadAllAsync())
                    {
                        var result = TrySetContext(datagram, context);

                        if (result.IsException())
                        {
                            await WriteToOutputAsync(result);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (inputChannel.Reader.Completion.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                inputChannel.Writer.TryComplete(e);

                await SendRemainingsAsync();
            }
            finally
            {
                inputCompletionSource.SetResult();
            }
        }

        private async ValueTask SendRemainingsAsync()
        {
            while (inputChannel.Reader.TryRead(out var datagram))
            {
                if (datagram.IsException())
                {
                    await WriteToOutputAsync(datagram);
                }
                else
                {
                    await WriteUnsentAsync(datagram);
                }
            }
        }

        private async ValueTask WriteUnsentAsync(Try<Datagram> datagram)
        {
            var exception = CreateSocketException(SocketError.OperationAborted, datagram.Value);

            await WriteToOutputAsync(new Try<Datagram>(exception));
        }

        private ValueTask WriteToOutputAsync(Try<Datagram> datagram)
        {
            return outputChannel.Writer.WriteAsync(datagram);
        }

        private Try<Datagram> TrySetContext(Try<Datagram> datagram, AsyncEnumeratorContext context)
        {
            if (datagram.IsException())
            {
                return datagram;
            }

            if (!datagram.Value.TryGetEndPoint(out var endPoint) && !datagram.Value.IsEndPointEmpty())
            {
                return new Try<Datagram>(CreateSocketException(SocketError.AddressNotAvailable, datagram.Value));
            }

            context.EndPoint = endPoint;

            if (!datagram.Value.Buffer.TryCopyTo(context.Buffer))
            {
                return new Try<Datagram>(CreateSocketException(SocketError.MessageSize, datagram.Value));
            }

            context.Length = datagram.Value.Buffer.Length;

            return datagram;
        }

        private SocketException CreateSocketException(SocketError error, Datagram datagram)
        {
            var result = new SocketException((int)error);

            result.Data["Datagram"] = datagram;

            return result;
        }

        private async Task StartCompletionAsync()
        {
            await inputCompletionSource.Task;

            outputCancellationSource.Cancel();

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
    }
}
