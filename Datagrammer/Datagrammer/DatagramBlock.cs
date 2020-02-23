using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer
{
    public sealed class DatagramBlock : IPropagatorBlock<Datagram, Datagram>, IReceivableSourceBlock<Datagram>
    {
        private const int NotInitializedState = 0;
        private const int InitializedState = 1;

        private readonly DatagramOptions options;
        private readonly IPropagatorBlock<Datagram, Datagram> sendingBuffer;
        private readonly IPropagatorBlock<Datagram, Datagram> receivingBuffer;
        private readonly ITargetBlock<Datagram> sendingAction;
        private readonly IPropagatorBlock<AwaitableSocketAsyncEventArgs, Datagram> receivingAction;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly TaskCompletionSource<int> initializationTaskSource;
        private readonly Socket socket;
        private readonly TaskScheduler taskScheduler;
        private readonly ConcurrentQueue<AwaitableSocketAsyncEventArgs> socketEventsPool;

        private int state = NotInitializedState;

        public DatagramBlock() : this(new DatagramOptions())
        {
        }

        public DatagramBlock(DatagramOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            socket = options.Socket ?? throw new ArgumentNullException(nameof(options.Socket));

            taskScheduler = options.TaskScheduler ?? throw new ArgumentNullException(nameof(options.TaskScheduler));

            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);

            initializationTaskSource = new TaskCompletionSource<int>();

            sendingBuffer = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = options.SendingBufferCapacity,
                TaskScheduler = taskScheduler,
                CancellationToken = cancellationTokenSource.Token
            });

            receivingBuffer = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = options.ReceivingBufferCapacity,
                TaskScheduler = taskScheduler,
                CancellationToken = cancellationTokenSource.Token
            });

            sendingAction = new ActionBlock<Datagram>(SendMessageAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.SendingParallelismDegree,
                MaxDegreeOfParallelism = options.SendingParallelismDegree,
                CancellationToken = cancellationTokenSource.Token,
                TaskScheduler = taskScheduler
            });

            receivingAction = new TransformBlock<AwaitableSocketAsyncEventArgs, Datagram>(ReceiveMessageAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.ReceivingParallelismDegree,
                MaxDegreeOfParallelism = options.ReceivingParallelismDegree,
                CancellationToken = cancellationTokenSource.Token,
                TaskScheduler = taskScheduler
            });

            socketEventsPool = new ConcurrentQueue<AwaitableSocketAsyncEventArgs>();

            Completion = CompleteAsync();
        }

        public void Start()
        {
            if(!TryStartInitialization())
            {
                return;
            }

            try
            {
                StartClientListening();
                StartProcessing();
                CompleteInitialization();
            }
            catch(Exception e)
            {
                FaultInitialization(e);
                Fault(e);
            }
        }

        public Task Initialization => initializationTaskSource.Task;

        private bool TryStartInitialization()
        {
            var previousState = Interlocked.CompareExchange(ref state, InitializedState, NotInitializedState);
            var isInitializationNeeded = previousState == NotInitializedState;
            return isInitializationNeeded;
        }

        private void StartClientListening()
        {
            if(!socket.IsBound)
            {
                socket.Bind(options.ListeningPoint ?? throw new ArgumentNullException(nameof(options.ListeningPoint)));
            }
        }

        private void CompleteInitialization()
        {
            initializationTaskSource.SetResult(0);
        }

        private void FaultInitialization(Exception e)
        {
            initializationTaskSource.SetException(e);
        }

        private void StartProcessing()
        {
            LinkSendingAction();
            LinkReceivingAction();
            StartMessageReceiving();
        }

        private void LinkSendingAction()
        {
            sendingBuffer.LinkTo(sendingAction);
        }

        private void LinkReceivingAction()
        {
            receivingAction.LinkTo(receivingBuffer);
        }

        private void StartMessageReceiving()
        {
            Task.Factory.StartNew(ProcessMessageReceivingAsync, cancellationTokenSource.Token, TaskCreationOptions.None, taskScheduler);
        }

        private async void ProcessMessageReceivingAsync()
        {
            while(true)
            {
                try
                {
                    var socketEvent = GetOrCreateSocketEvent();
                    
                    if (!await receivingAction.SendAsync(socketEvent, cancellationTokenSource.Token))
                    {
                        break;
                    }
                }
                catch(Exception e)
                {
                    Fault(e);

                    throw;
                }
            }
        }

        private async Task<Datagram> ReceiveMessageAsync(AwaitableSocketAsyncEventArgs socketEvent)
        {
            try
            {
                if(socket.ReceiveAsync(socketEvent))
                {
                    await socketEvent;
                }
                else
                {
                    socketEvent.ThrowIfNotSuccess();
                }

                return socketEvent.GetDatagram();
            }
            catch(Exception e)
            {
                Fault(e);

                throw;
            }
            finally
            {
                ReleaseSocketEvent(socketEvent);
            }
        }

        public Task Completion { get; private set; }

        private async Task CompleteAsync()
        {
            try
            {
                await AwaitAllCompletions();
            }
            finally
            {
                DisposeSocketIfNeeded();
            }
        }

        private async Task AwaitAllCompletions()
        {
            await Task.WhenAll(initializationTaskSource.Task,
                               sendingBuffer.Completion,
                               sendingAction.Completion,
                               receivingBuffer.Completion,
                               receivingAction.Completion);
        }

        private void DisposeSocketIfNeeded()
        {
            if (options.DisposeSocketAfterCompletion)
            {
                socket?.Dispose();
            }
        }

        public void Complete()
        {
            receivingBuffer.Complete();
            sendingBuffer.Complete();
            sendingAction.Complete();
            receivingAction.Complete();
        }

        public Datagram ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<Datagram> target, out bool messageConsumed)
        {
            return receivingBuffer.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void Fault(Exception exception)
        {
            receivingBuffer.Fault(exception);
            sendingBuffer.Fault(exception);
            sendingAction.Fault(exception);
            receivingAction.Fault(exception);
        }

        private async Task SendMessageAsync(Datagram message)
        {
            var socketEvent = GetOrCreateSocketEvent();

            try
            {
                socketEvent.SetDatagram(message);

                if (socket.SendToAsync(socketEvent))
                {
                    await socketEvent;
                }
                else
                {
                    socketEvent.ThrowIfNotSuccess();
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
            finally
            {
                ReleaseSocketEvent(socketEvent);
            }
        }

        private AwaitableSocketAsyncEventArgs GetOrCreateSocketEvent()
        {
            if(socketEventsPool.TryDequeue(out var socketEvent))
            {
                return socketEvent;
            }

            return new AwaitableSocketAsyncEventArgs();
        }

        private void ReleaseSocketEvent(AwaitableSocketAsyncEventArgs socketEvent)
        {
            socketEvent.Reset();

            socketEventsPool.Enqueue(socketEvent);
        }

        public IDisposable LinkTo(ITargetBlock<Datagram> target, DataflowLinkOptions linkOptions)
        {
            return receivingBuffer.LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<Datagram> target)
        {
            receivingBuffer.ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<Datagram> target)
        {
            return receivingBuffer.ReserveMessage(messageHeader, target);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, Datagram messageValue, ISourceBlock<Datagram> source, bool consumeToAccept)
        {
            return sendingBuffer.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public bool TryReceive(Predicate<Datagram> filter, out Datagram item)
        {
            var receivable = (IReceivableSourceBlock<Datagram>)receivingBuffer;

            return receivable.TryReceive(filter, out item);
        }

        public bool TryReceiveAll(out IList<Datagram> items)
        {
            var receivable = (IReceivableSourceBlock<Datagram>)receivingBuffer;

            return receivable.TryReceiveAll(out items);
        }
    }
}
