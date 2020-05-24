using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Dataflow
{
    public sealed class DatagramBlock : IPropagatorBlock<Datagram, Datagram>
    {
        private readonly BufferBlock<Datagram> sendingBuffer;
        private readonly BufferBlock<Datagram> receivingBuffer;
        private readonly Channel<Datagram> channel;
        private readonly TaskScheduler taskScheduler;
        private readonly CancellationTokenSource completionCancellationSource;
        private readonly CancellationTokenSource faultCancellationSource;
        private readonly bool needChannelCompletion;

        private DatagramBlock(Channel<Datagram> channel, DatagramBlockOptions options)
        {
            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.channel = channel ?? throw new ArgumentNullException(nameof(channel));

            taskScheduler = options.TaskScheduler ?? throw new ArgumentNullException(nameof(options.TaskScheduler));

            completionCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
            faultCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);

            needChannelCompletion = options.CompleteChannel;

            sendingBuffer = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = options.SendingBufferCapacity,
                TaskScheduler = options.TaskScheduler,
                CancellationToken = options.CancellationToken
            });

            receivingBuffer = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = options.ReceivingBufferCapacity,
                TaskScheduler = options.TaskScheduler,
                CancellationToken = options.CancellationToken
            });
        }

        public static DatagramBlock Start(Channel<Datagram> channel, Action<DatagramBlockOptions> configuration = null)
        {
            var options = new DatagramBlockOptions();

            configuration?.Invoke(options);

            var datagramBlock = new DatagramBlock(channel, options);

            datagramBlock.Start();

            return datagramBlock;
        }

        private void Start()
        {
            StartAsyncActions(
                CompleteByChannelAsync,
                CompleteChannelIfNeededAsync,
                StartMessageReceivingAsync,
                StartMessageSendingAsync);
        }

        private async Task StartMessageSendingAsync()
        {
            try
            {
                while (await sendingBuffer.OutputAvailableAsync())
                {
                    while (sendingBuffer.TryReceive(out var message))
                    {
                        while (!channel.Writer.TryWrite(message))
                        {
                            if (!await channel.Writer.WaitToWriteAsync(faultCancellationSource.Token))
                            {
                                return;
                            }
                        }
                    }
                }
            }
            finally
            {
                DropSendingMessages();
            }
        }

        private async Task StartMessageReceivingAsync()
        {
            try
            {
                while (await channel.Reader.WaitToReadAsync(completionCancellationSource.Token))
                {
                    while (!completionCancellationSource.IsCancellationRequested && channel.Reader.TryRead(out var message))
                    {
                        if (!receivingBuffer.Post(message))
                        {
                            if (!await receivingBuffer.SendAsync(message, faultCancellationSource.Token))
                            {
                                return;
                            }
                        }
                    }
                }
            }
            finally
            {
                await CompleteReceivingAsync();
            }
        }

        private async Task CompleteByChannelAsync()
        {
            try
            {
                await channel.Reader.Completion;

                Complete();
            }
            catch(Exception e)
            {
                Fault(e);
            }
        }

        private async Task CompleteChannelIfNeededAsync()
        {
            if (!needChannelCompletion)
            {
                return;
            }

            try
            {
                await Completion;

                channel.Writer.TryComplete();
            }
            catch (Exception e)
            {
                channel.Writer.TryComplete(e);
            }
        }

        private async ValueTask CompleteReceivingAsync()
        {
            try
            {
                await sendingBuffer.Completion;

                receivingBuffer.Complete();
            }
            catch (Exception e)
            {
                (receivingBuffer as IDataflowBlock).Fault(e);
            }
        }

        private void StartAsyncActions(params Func<Task>[] asyncActions)
        {
            foreach(var action in asyncActions)
            {
                Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, taskScheduler);
            }
        }

        private void DropSendingMessages()
        {
            sendingBuffer.LinkTo(DataflowBlock.NullTarget<Datagram>());
        }

        public Task Completion => Task.WhenAll(sendingBuffer.Completion, receivingBuffer.Completion);

        public void Complete()
        {
            sendingBuffer.Complete();
            completionCancellationSource.Cancel();
        }

        public void Fault(Exception exception)
        {
            (sendingBuffer as IDataflowBlock).Fault(exception);
            completionCancellationSource.Cancel();
            faultCancellationSource.Cancel();
        }

        public Datagram ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<Datagram> target, out bool messageConsumed)
        {
            return (receivingBuffer as ISourceBlock<Datagram>).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public IDisposable LinkTo(ITargetBlock<Datagram> target, DataflowLinkOptions linkOptions)
        {
            return (receivingBuffer as ISourceBlock<Datagram>).LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<Datagram> target)
        {
            (receivingBuffer as ISourceBlock<Datagram>).ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<Datagram> target)
        {
            return (receivingBuffer as ISourceBlock<Datagram>).ReserveMessage(messageHeader, target);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, Datagram messageValue, ISourceBlock<Datagram> source, bool consumeToAccept)
        {
            return (sendingBuffer as ITargetBlock<Datagram>).OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }
    }
}
