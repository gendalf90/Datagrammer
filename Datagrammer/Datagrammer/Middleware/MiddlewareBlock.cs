using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Middleware
{
    public abstract class MiddlewareBlock : IPropagatorBlock<Datagram, Datagram>
    {
        private readonly MiddlewareOptions options;
        private readonly IPropagatorBlock<Datagram, Datagram> inputBuffer;
        private readonly ITargetBlock<Datagram> processingAction;
        private readonly IPropagatorBlock<Datagram, Datagram> outputBuffer;

        public MiddlewareBlock(MiddlewareOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            inputBuffer = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = options.InputBufferCapacity
            });

            processingAction = new ActionBlock<Datagram>(ProcessAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.ProcessingParallelismDegree,
                MaxDegreeOfParallelism = options.ProcessingParallelismDegree
            });

            outputBuffer = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = options.OutputBufferCapacity
            });

            inputBuffer.LinkTo(processingAction);
        }

        protected async Task NextAsync(Datagram datagram)
        {
            await outputBuffer.SendAsync(datagram);
        }

        protected abstract Task ProcessAsync(Datagram datagram);

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

        public Datagram ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<Datagram> target, out bool messageConsumed)
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

        public IDisposable LinkTo(ITargetBlock<Datagram> target, DataflowLinkOptions linkOptions)
        {
            return outputBuffer.LinkTo(target, linkOptions);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, Datagram messageValue, ISourceBlock<Datagram> source, bool consumeToAccept)
        {
            return inputBuffer.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<Datagram> target)
        {
            outputBuffer.ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<Datagram> target)
        {
            return outputBuffer.ReserveMessage(messageHeader, target);
        }
    }
}
