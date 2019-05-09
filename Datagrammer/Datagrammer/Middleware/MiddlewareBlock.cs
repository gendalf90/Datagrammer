﻿using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Middleware
{
    public abstract class MiddlewareBlock<TInput, TOutput> : IPropagatorBlock<TInput, TOutput>
    {
        private readonly IPropagatorBlock<TInput, TInput> inputBuffer;
        private readonly ITargetBlock<TInput> processingAction;
        private readonly IPropagatorBlock<TOutput, TOutput> outputBuffer;

        public MiddlewareBlock(MiddlewareOptions options)
        {
            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            inputBuffer = new BufferBlock<TInput>(new DataflowBlockOptions
            {
                BoundedCapacity = options.InputBufferCapacity
            });

            processingAction = new ActionBlock<TInput>(ProcessSafeAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.ProcessingParallelismDegree,
                MaxDegreeOfParallelism = options.ProcessingParallelismDegree
            });

            outputBuffer = new BufferBlock<TOutput>(new DataflowBlockOptions
            {
                BoundedCapacity = options.OutputBufferCapacity
            });

            inputBuffer.LinkTo(processingAction);
        }

        protected async Task NextAsync(TOutput value)
        {
            await outputBuffer.SendAsync(value);
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

        public Task Completion => Task.WhenAll(inputBuffer.Completion,
                                               processingAction.Completion, 
                                               outputBuffer.Completion,
                                               AwaitCompletionAsync());

        protected virtual Task AwaitCompletionAsync()
        {
            return Task.CompletedTask;
        }

        public void Complete()
        {
            inputBuffer.Complete();
            processingAction.Complete();
            outputBuffer.Complete();
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
            processingAction.Fault(exception);
            outputBuffer.Fault(exception);
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