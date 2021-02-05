using Datagrammer.Channels;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Dataflow
{
    internal sealed class DatagramBlock : IPropagatorBlock<Try<Datagram>, Try<Datagram>>
    {
        private readonly TaskFactory taskFactory;
        private readonly BufferBlock<Try<Datagram>> inputBuffer;
        private readonly BufferBlock<Try<Datagram>> outputBuffer;
        private readonly DatagramChannel channel;
        private readonly TaskCompletionSource inputCompletionSource;
        private readonly TaskCompletionSource outputCompletionSource;

        public DatagramBlock(IDatagramSocket socket, DatagramBlockOptions options)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            taskFactory = new TaskFactory(options.TaskScheduler ?? TaskScheduler.Default);

            channel = new DatagramChannel(socket, new DatagramChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

            inputBuffer = new BufferBlock<Try<Datagram>>(new DataflowBlockOptions
            {
                BoundedCapacity = options.SendingBufferCapacity ?? 1,
                TaskScheduler = options.TaskScheduler ?? TaskScheduler.Default,
                CancellationToken = options.CancellationToken ?? CancellationToken.None,
                EnsureOrdered = false
            });

            outputBuffer = new BufferBlock<Try<Datagram>>(new DataflowBlockOptions
            {
                BoundedCapacity = options.ReceivingBufferCapacity ?? 1,
                TaskScheduler = options.TaskScheduler ?? TaskScheduler.Default,
                CancellationToken = options.CancellationToken ?? CancellationToken.None,
                EnsureOrdered = false
            });

            inputCompletionSource = new TaskCompletionSource();
            outputCompletionSource = new TaskCompletionSource();
        }

        public void Start()
        {
            taskFactory.StartNew(channel.Start);
            taskFactory.StartNew(StartMessageInputAsync);
            taskFactory.StartNew(StartMessageOutputAsync);
            taskFactory.StartNew(CompleteByChannelAsync);
            taskFactory.StartNew(CompleteInputAsync);
        }

        private async Task StartMessageInputAsync()
        {
            try
            {
                while (await inputBuffer.OutputAvailableAsync())
                {
                    while (inputBuffer.TryReceive(out var message))
                    {
                        if (message.IsException())
                        {
                            await WriteMessageAsync(message);
                        }
                        else
                        {
                            await SendMessageAsync(message);
                        }
                    }
                }
            }
            finally
            {
                inputCompletionSource.SetResult();
            }
        }

        private async Task SendMessageAsync(Try<Datagram> message)
        {
            if (!channel.Writer.TryWrite(message))
            {
                await channel.Writer.WriteAsync(message);
            }
        }

        private async Task WriteMessageAsync(Try<Datagram> message)
        {
            if (!outputBuffer.Post(message))
            {
                await outputBuffer.SendAsync(message);
            }
        }

        private async Task StartMessageOutputAsync()
        {
            try
            {
                while (await channel.Reader.WaitToReadAsync())
                {
                    while (channel.Reader.TryRead(out var message))
                    {
                        try
                        {
                            await WriteMessageAsync(message);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }
            finally
            {
                outputCompletionSource.SetResult();
            }
        }

        private async Task CompleteByChannelAsync()
        {
            await outputCompletionSource.Task;

            try
            {
                await channel.Reader.Completion;

                inputBuffer.Complete();
                outputBuffer.Complete();
            }
            catch(Exception e)
            {
                (inputBuffer as IDataflowBlock).Fault(e);
                (outputBuffer as IDataflowBlock).Fault(e);
            }
        }

        private async Task CompleteInputAsync()
        {
            await inputCompletionSource.Task;

            try
            {
                await inputBuffer.Completion;

                channel.Writer.Complete();
            }
            catch (Exception e)
            {
                channel.Writer.Complete(e);
            }
        }

        public Task Completion => Task.WhenAll(inputBuffer.Completion, outputBuffer.Completion);

        public void Complete()
        {
            inputBuffer.Complete();
        }

        public void Fault(Exception exception)
        {
            (inputBuffer as IDataflowBlock).Fault(exception);
            (outputBuffer as IDataflowBlock).Fault(exception);
        }

        public Try<Datagram> ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<Try<Datagram>> target, out bool messageConsumed)
        {
            return (outputBuffer as ISourceBlock<Try<Datagram>>).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public IDisposable LinkTo(ITargetBlock<Try<Datagram>> target, DataflowLinkOptions linkOptions)
        {
            return (outputBuffer as ISourceBlock<Try<Datagram>>).LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<Try<Datagram>> target)
        {
            (outputBuffer as ISourceBlock<Try<Datagram>>).ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<Try<Datagram>> target)
        {
            return (outputBuffer as ISourceBlock<Try<Datagram>>).ReserveMessage(messageHeader, target);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, Try<Datagram> messageValue, ISourceBlock<Try<Datagram>> source, bool consumeToAccept)
        {
            return (inputBuffer as ITargetBlock<Try<Datagram>>).OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }
    }
}
