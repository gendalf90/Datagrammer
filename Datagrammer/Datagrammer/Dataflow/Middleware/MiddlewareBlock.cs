using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Dataflow.Middleware
{
    public abstract class MiddlewareBlock<TInput, TOutput> : IPropagatorBlock<TInput, TOutput>
    {
        private readonly ITargetBlock<TInput> processingAction;
        private readonly IPropagatorBlock<TOutput, TOutput> outputBuffer;
        private readonly CancellationToken cancellationToken;

        public MiddlewareBlock(MiddlewareOptions options)
        {
            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if(options.TaskScheduler == null)
            {
                throw new ArgumentNullException(nameof(options.TaskScheduler));
            }

            cancellationToken = options.CancellationToken;

            processingAction = new ActionBlock<TInput>(ProcessAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.InputBufferCapacity,
                MaxDegreeOfParallelism = options.ProcessingParallelismDegree,
                TaskScheduler = options.TaskScheduler,
                CancellationToken = options.CancellationToken
            });

            outputBuffer = new BufferBlock<TOutput>(new DataflowBlockOptions
            {
                BoundedCapacity = options.OutputBufferCapacity,
                TaskScheduler = options.TaskScheduler
            });

            Task.WhenAll(processingAction.Completion, InnerCompletion).ContinueWith(task =>
            {
                if (task.Exception == null)
                {
                    outputBuffer.Complete();
                }
                else
                {
                    outputBuffer.Fault(task.Exception);
                }
            }, options.TaskScheduler);
        }

        protected async Task NextAsync(TOutput value)
        {
            await outputBuffer.SendAsync(value, cancellationToken);
        }

        protected abstract Task ProcessAsync(TInput value);

        public Task Completion => Task.WhenAll(InnerCompletion,
                                               processingAction.Completion,
                                               outputBuffer.Completion);

        public void Complete()
        {
            processingAction.Complete();
            OnComplete();
        }

        public void Fault(Exception exception)
        {
            processingAction.Fault(exception);
            OnFault(exception);
        }

        protected virtual Task InnerCompletion => Task.CompletedTask;

        protected virtual void OnComplete()
        {
        }

        protected virtual void OnFault(Exception exception)
        {
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TInput messageValue, ISourceBlock<TInput> source, bool consumeToAccept)
        {
            return processingAction.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public IDisposable LinkTo(ITargetBlock<TOutput> target, DataflowLinkOptions linkOptions)
        {
            return outputBuffer.LinkTo(target, linkOptions);
        }

        public TOutput ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target, out bool messageConsumed)
        {
            return outputBuffer.ConsumeMessage(messageHeader, target, out messageConsumed);
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
