using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Middleware
{
    public abstract class MiddlewareBlock<TInput, TOutput> : IPropagatorBlock<TInput, TOutput>
    {
        private readonly IPropagatorBlock<TInput, TInput> inputBuffer;
        private readonly ITargetBlock<TInput> processingAction;
        private readonly IPropagatorBlock<TOutput, TOutput> outputBuffer;
        private readonly MiddlewareOptions options;

        public MiddlewareBlock(MiddlewareOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            inputBuffer = new BufferBlock<TInput>(new DataflowBlockOptions
            {
                BoundedCapacity = options.InputBufferCapacity,
                TaskScheduler = options.TaskScheduler,
                CancellationToken = options.CancellationToken
            });

            processingAction = new ActionBlock<TInput>(ProcessSafeAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.ProcessingParallelismDegree,
                MaxDegreeOfParallelism = options.ProcessingParallelismDegree,
                TaskScheduler = options.TaskScheduler,
                CancellationToken = options.CancellationToken,
                SingleProducerConstrained = true
            });

            outputBuffer = new BufferBlock<TOutput>(new DataflowBlockOptions
            {
                BoundedCapacity = options.OutputBufferCapacity,
                TaskScheduler = options.TaskScheduler,
                CancellationToken = options.CancellationToken
            });

            StartProcessing();
        }

        private void StartProcessing()
        {
            Task.Factory.StartNew(() =>
            {
                LinkProcessingAction();
                Task.Factory.StartNew(CompleteOutputBufferAsync);
            }, CancellationToken.None, TaskCreationOptions.None, options.TaskScheduler);
        }

        private void LinkProcessingAction()
        {
            inputBuffer.LinkTo(processingAction, new DataflowLinkOptions { PropagateCompletion = true });
        }

        private async Task CompleteOutputBufferAsync()
        {
            try
            {
                await Task.WhenAll(processingAction.Completion, InnerCompletion);

                outputBuffer.Complete();
            }
            catch(Exception e)
            {
                outputBuffer.Fault(e);
            }
        }

        protected async Task NextAsync(TOutput value)
        {
            await outputBuffer.SendAsync(value, options.CancellationToken);
        }

        private async Task ProcessSafeAsync(TInput value)
        {
            try
            {
                await ProcessAsync(value);
            }
            catch(Exception e)
            {
                Fault(e);
            }
        }

        protected abstract Task ProcessAsync(TInput value);

        public Task Completion => Task.WhenAll(InnerCompletion,
                                               processingAction.Completion,
                                               outputBuffer.Completion);

        protected virtual Task InnerCompletion => Task.CompletedTask;

        public void Complete()
        {
            inputBuffer.Complete();
            OnComplete();
        }

        protected virtual void OnComplete()
        {
        }

        public TOutput ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target, out bool messageConsumed)
        {
            return outputBuffer.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void Fault(Exception exception)
        {
            inputBuffer.Fault(exception);
            OnFault(exception);
        }

        protected virtual void OnFault(Exception exception)
        {
        }

        public IDisposable LinkTo(ITargetBlock<TOutput> target, DataflowLinkOptions linkOptions)
        {
            return outputBuffer.LinkTo(target, linkOptions);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TInput messageValue, ISourceBlock<TInput> source, bool consumeToAccept)
        {
            return inputBuffer.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target)
        {
            outputBuffer.ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target)
        {
            return outputBuffer.ReserveMessage(messageHeader, target);
        }
    }
}
