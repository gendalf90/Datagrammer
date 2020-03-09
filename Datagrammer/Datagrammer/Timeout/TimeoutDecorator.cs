using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Timeout
{
    public sealed class TimeoutDecorator<T> : ITargetBlock<T>
    {
        private readonly ITargetBlock<T> targetBlock;
        private readonly IPropagatorBlock<T, T> bufferBlock;
        private readonly TimeoutOptions options;

        public TimeoutDecorator(ITargetBlock<T> targetBlock) : this(targetBlock, new TimeoutOptions())
        {
        }

        public TimeoutDecorator(ITargetBlock<T> targetBlock, TimeoutOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            this.targetBlock = targetBlock ?? throw new ArgumentNullException(nameof(targetBlock));

            bufferBlock = new BufferBlock<T>(new DataflowBlockOptions
            {
                BoundedCapacity = options.BufferCapacity,
                CancellationToken = options.CancellationToken,
                TaskScheduler = options.TaskScheduler
            });

            StartProcessing();
        }

        private void StartProcessing()
        {
            Task.Factory.StartNew(() =>
            {
                Task.Factory.StartNew(CompleteBufferAsync);
                Task.Factory.StartNew(StartConsumingAsync);
            }, CancellationToken.None, TaskCreationOptions.None, options.TaskScheduler);
        }

        private async Task StartConsumingAsync()
        {
            try
            {
                while(true)
                {
                    await PerformConsumingAsync();
                }
            }
            catch(InvalidOperationException)
            {
                await CompleteTargetAsync();
            }
            catch(Exception e)
            {
                FaultAll(e);
            }
        }

        private async Task PerformConsumingAsync()
        {
            var value = await bufferBlock.ReceiveAsync(options.Timeout, options.CancellationToken);

            await targetBlock.SendAsync(value, options.CancellationToken);
        }

        private async Task CompleteTargetAsync()
        {
            try
            {
                await bufferBlock.Completion;

                targetBlock.Complete();
            }
            catch(Exception e)
            {
                targetBlock.Fault(e);
            }
        }

        public Task Completion => Task.WhenAll(targetBlock.Completion, bufferBlock.Completion);

        public void Complete()
        {
            bufferBlock.Complete();
        }

        private async Task CompleteBufferAsync()
        {
            try
            {
                await targetBlock.Completion;

                bufferBlock.Complete();
            }
            catch (Exception e)
            {
                bufferBlock.Fault(e);
            }
        }

        public void Fault(Exception exception)
        {
            bufferBlock.Fault(exception);
        }

        private void FaultAll(Exception exception)
        {
            bufferBlock.Fault(exception);
            targetBlock.Fault(exception);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, T messageValue, ISourceBlock<T> source, bool consumeToAccept)
        {
            return bufferBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }
    }
}
