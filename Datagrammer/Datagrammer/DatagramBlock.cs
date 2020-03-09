using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer
{
    public sealed class DatagramBlock : IPropagatorBlock<Datagram, Datagram>
    {
        private const int NotInitializedState = 0;
        private const int InitializedState = 1;

        private readonly DatagramOptions options;
        private readonly IPropagatorBlock<Datagram, Datagram> sendingBuffer;
        private readonly IPropagatorBlock<Datagram, Datagram> receivingBuffer;
        private readonly ITargetBlock<Datagram> sendingAction;
        private readonly ITargetBlock<AwaitableSocketAsyncEventArgs> receivingAction;
        private readonly CancellationTokenSource receivingCancellationTokenSource;
        private readonly TaskCompletionSource<int> initializationTaskSource;
        private readonly Socket socket;
        private readonly ConcurrentQueue<AwaitableSocketAsyncEventArgs> socketEventsPool;

        private int state = NotInitializedState;

        public DatagramBlock() : this(new DatagramOptions())
        {
        }

        public DatagramBlock(DatagramOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            socket = options.Socket ?? throw new ArgumentNullException(nameof(options.Socket));

            receivingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);

            initializationTaskSource = new TaskCompletionSource<int>();
            
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

            sendingAction = new ActionBlock<Datagram>(SendMessageAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.SendingParallelismDegree,
                MaxDegreeOfParallelism = options.SendingParallelismDegree,
                CancellationToken = options.CancellationToken,
                TaskScheduler = options.TaskScheduler,
                SingleProducerConstrained = true
            });

            receivingAction = new ActionBlock<AwaitableSocketAsyncEventArgs>(ReceiveMessageAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.ReceivingParallelismDegree,
                MaxDegreeOfParallelism = options.ReceivingParallelismDegree,
                CancellationToken = options.CancellationToken,
                TaskScheduler = options.TaskScheduler,
                SingleProducerConstrained = true
            });

            socketEventsPool = new ConcurrentQueue<AwaitableSocketAsyncEventArgs>();
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
            return previousState == NotInitializedState;
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
            Task.Factory.StartNew(() =>
            {
                LinkSendingAction();
                Task.Factory.StartNew(CompleteReceivingBufferAsync);
                Task.Factory.StartNew(DisposeSocketIfNeededAsync);
                Task.Factory.StartNew(ProcessMessageReceivingAsync);

            }, CancellationToken.None, TaskCreationOptions.None, options.TaskScheduler);
        }

        private void LinkSendingAction()
        {
            sendingBuffer.LinkTo(sendingAction, new DataflowLinkOptions { PropagateCompletion = true });
        }

        private async Task CompleteReceivingBufferAsync()
        {
            try
            {
                await receivingAction.Completion;

                receivingBuffer.Complete();
            }
            catch(Exception e)
            {
                receivingBuffer.Fault(e);
            }
        }

        private async Task DisposeSocketIfNeededAsync()
        {
            if(!options.DisposeSocketAfterCompletion)
            {
                return;
            }

            using (socket)
            {
                await Completion;
            }
        }

        private async Task ProcessMessageReceivingAsync()
        {
            try
            {
                while(true)
                {
                    await PerformMessageReceivingAsync();
                }
            }
            catch(Exception e)
            {
                Fault(e);
            }
        }

        private async Task PerformMessageReceivingAsync()
        {
            var socketEvent = GetOrCreateSocketEvent();

            await receivingAction.SendAsync(socketEvent, receivingCancellationTokenSource.Token);
        }

        private async Task ReceiveMessageAsync(AwaitableSocketAsyncEventArgs socketEvent)
        {
            try
            {
                socketEvent.SetAnyEndPoint(socket.AddressFamily);

                if(socket.ReceiveFromAsync(socketEvent))
                {
                    await socketEvent.WaitUntilCompletedAsync(receivingCancellationTokenSource.Token);
                }
                else
                {
                    socketEvent.ThrowIfNotSuccess();
                }

                var message = socketEvent.GetDatagram();

                ReleaseSocketEvent(socketEvent);

                await receivingBuffer.SendAsync(message, options.CancellationToken);
            }
            catch(SocketException e)
            {
                ReleaseSocketEvent(socketEvent);

                await HandleSocketErrorAsync(e);
            }
            catch(Exception e)
            {
                Fault(e);
            }
        }

        public Task Completion => Task.WhenAll(initializationTaskSource.Task,
                                               sendingBuffer.Completion,
                                               sendingAction.Completion,
                                               receivingAction.Completion,
                                               receivingBuffer.Completion);

        public void Complete()
        {
            sendingBuffer.Complete();
            receivingAction.Complete();
            receivingCancellationTokenSource.Cancel();
        }

        public Datagram ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<Datagram> target, out bool messageConsumed)
        {
            return receivingBuffer.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void Fault(Exception exception)
        {
            sendingBuffer.Fault(exception);
            receivingAction.Fault(exception);
            receivingCancellationTokenSource.Cancel();
        }

        private async Task SendMessageAsync(Datagram message)
        {
            AwaitableSocketAsyncEventArgs socketEvent = null;

            try
            {
                socketEvent = GetOrCreateSocketEvent();

                socketEvent.SetDatagram(message);

                if (socket.SendToAsync(socketEvent))
                {
                    await socketEvent.WaitUntilCompletedAsync(options.CancellationToken);
                }
                else
                {
                    socketEvent.ThrowIfNotSuccess();
                }

                ReleaseSocketEvent(socketEvent);
            }
            catch (SocketException e)
            {
                ReleaseSocketEvent(socketEvent);

                await HandleSocketErrorAsync(e);
            }
            catch (Exception e)
            {
                Fault(e);
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

        private async Task HandleSocketErrorAsync(SocketException socketException)
        {
            try
            {
                var handler = options.SocketErrorHandler;

                if(handler != null)
                {
                    await handler(socketException);
                }
            }
            catch(Exception e)
            {
                Fault(e);
            }
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
    }
}
