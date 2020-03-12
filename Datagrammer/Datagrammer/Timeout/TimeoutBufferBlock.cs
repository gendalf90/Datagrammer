using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Timeout
{
    public sealed class TimeoutBufferBlock<T> : IPropagatorBlock<T, T>
    {
        private readonly IPropagatorBlock<T, T> inputBuffer;
        private readonly IPropagatorBlock<T, T> outputBuffer;
        private readonly TimeSpan timeout;
        private readonly TimeoutOptions options;

        public TimeoutBufferBlock(TimeSpan timeout) : this(timeout, new TimeoutOptions())
        {
        }

        public TimeoutBufferBlock(TimeSpan timeout, TimeoutOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            this.timeout = timeout;

            inputBuffer = new BufferBlock<T>(new DataflowBlockOptions
            {
                BoundedCapacity = options.InputBufferCapacity,
                CancellationToken = options.CancellationToken,
                TaskScheduler = options.TaskScheduler
            });

            outputBuffer = new BufferBlock<T>(new DataflowBlockOptions
            {
                BoundedCapacity = options.OutputBufferCapacity,
                CancellationToken = options.CancellationToken,
                TaskScheduler = options.TaskScheduler
            });

            StartProcessing();
        }

        private void StartProcessing()
        {
            Task.Factory.StartNew(StartConsumingAsync, CancellationToken.None, TaskCreationOptions.None, options.TaskScheduler);
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
            catch(Exception e)
            {
                Fault(e);
            }
        }

        private async Task PerformConsumingAsync()
        {
            var value = await inputBuffer.ReceiveAsync(timeout, options.CancellationToken);

            await outputBuffer.SendAsync(value, options.CancellationToken);
        }

        public Task Completion => Task.WhenAll(inputBuffer.Completion, outputBuffer.Completion);

        public void Complete()
        {
            outputBuffer.Complete();
            inputBuffer.Complete();
        }

        public void Fault(Exception exception)
        {
            outputBuffer.Fault(exception);
            inputBuffer.Fault(exception);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, T messageValue, ISourceBlock<T> source, bool consumeToAccept)
        {
            return inputBuffer.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public IDisposable LinkTo(ITargetBlock<T> target, DataflowLinkOptions linkOptions)
        {
            return outputBuffer.LinkTo(target, linkOptions);
        }

        public T ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target, out bool messageConsumed)
        {
            return outputBuffer.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target)
        {
            return outputBuffer.ReserveMessage(messageHeader, target);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<T> target)
        {
            outputBuffer.ReleaseReservation(messageHeader, target);
        }
    }
}
