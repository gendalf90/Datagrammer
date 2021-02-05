using Datagrammer.AsyncEnumerables;
using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Datagrammer.Channels
{
    internal sealed class DatagramChannel : Channel<Try<Datagram>>
    {
        private readonly IDatagramSocket socket;
        private readonly Channel<Try<Datagram>> inputChannel;
        private readonly Channel<Try<Datagram>> outputChannel;
        private readonly TaskCompletionSource inputCompletionSource;
        private readonly TaskCompletionSource outputCompletionSource;
        private readonly CancellationTokenSource outputCancellationSource;

        public DatagramChannel(IDatagramSocket socket, DatagramChannelOptions options)
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
                context.Buffer.ToArray(), 
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
                        if (datagram.IsException())
                        {
                            await WriteToOutputAsync(datagram);
                        }
                        else if (await TrySetContextAsync(datagram, context))
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
            var error = new SocketException((int)SocketError.OperationAborted);

            error.Data["Datagram"] = datagram.Value;

            await WriteToOutputAsync(new Try<Datagram>(error));
        }

        private ValueTask WriteToOutputAsync(Try<Datagram> datagram)
        {
            return outputChannel.Writer.WriteAsync(datagram);
        }

        private async ValueTask<bool> TrySetContextAsync(Try<Datagram> datagram, AsyncEnumeratorContext context)
        {
            try
            {
                SetContext(datagram, context);

                return true;
            }
            catch (SocketException e)
            {
                await WriteToOutputAsync(new Try<Datagram>(e));

                return false;
            }
        }

        private void SetContext(Try<Datagram> datagram, AsyncEnumeratorContext context)
        {
            try
            {
                context.EndPoint = datagram.Value.GetEndPoint();
            }
            catch
            {
                throw new SocketException((int)SocketError.AddressNotAvailable);
            }

            if (datagram.Value.Buffer.Length > DatagramBuffer.MaxSize)
            {
                throw new SocketException((int)SocketError.MessageSize);
            }

            context.Buffer = MemoryMarshal.AsMemory(datagram.Value.Buffer);
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
