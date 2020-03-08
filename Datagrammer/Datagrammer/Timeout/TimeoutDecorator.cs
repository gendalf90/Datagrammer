using System;
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

            Task.Factory.StartNew(StartProcessing, options.CancellationToken, TaskCreationOptions.None, options.TaskScheduler);
        }

        private void StartProcessing()
        {
            CompleteAsync();
            StartConsumingAsync();
        }

        private async void StartConsumingAsync()
        {
            try
            {
                while(true)
                {
                    var value = await bufferBlock.ReceiveAsync(options.Timeout, options.CancellationToken);

                    await targetBlock.SendAsync(value, options.CancellationToken);
                }
            }
            catch(OperationCanceledException e)
            {
                FaultAll(e);
            }
        }

        public Task Completion => Task.WhenAll(targetBlock.Completion, bufferBlock.Completion);

        public void Complete()
        {
            bufferBlock.Complete();
        }

        private void CompleteAll()
        {
            bufferBlock.Complete();
            targetBlock.Complete();
        }

        private async void CompleteAsync()
        {
            try
            {
                await Task.WhenAny(targetBlock.Completion, bufferBlock.Completion);

                CompleteAll();
            }
            catch (Exception e)
            {
                FaultAll(e);
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
