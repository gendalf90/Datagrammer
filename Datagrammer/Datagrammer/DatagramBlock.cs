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
        private readonly CancellationTokenSource processingCancellationTokenSource;
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

            processingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);

            initializationTaskSource = new TaskCompletionSource<int>();
            
            sendingBuffer = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = options.SendingBufferCapacity,
                TaskScheduler = taskScheduler,
                CancellationToken = options.CancellationToken
            });

            receivingBuffer = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = options.ReceivingBufferCapacity,
                TaskScheduler = taskScheduler,
                CancellationToken = options.CancellationToken
            });

            sendingAction = new ActionBlock<Datagram>(SendMessageAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.SendingParallelismDegree,
                MaxDegreeOfParallelism = options.SendingParallelismDegree,
                CancellationToken = options.CancellationToken,
                TaskScheduler = taskScheduler
            });

            receivingAction = new ActionBlock<AwaitableSocketAsyncEventArgs>(ReceiveMessageAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.ReceivingParallelismDegree,
                MaxDegreeOfParallelism = options.ReceivingParallelismDegree,
                CancellationToken = options.CancellationToken,
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
            LinkSendingAction();
            StartMessageReceiving();
        }

        private void LinkSendingAction()
        {
            sendingBuffer.LinkTo(sendingAction);
        }

        private void StartMessageReceiving()
        {
            Task.Factory.StartNew(ProcessMessageReceivingAsync, processingCancellationTokenSource.Token, TaskCreationOptions.None, taskScheduler);
        }

        private async void ProcessMessageReceivingAsync()
        {
            try
            {
                while(true)
                {
                    var socketEvent = GetOrCreateSocketEvent();

                    await receivingAction.SendAsync(socketEvent, processingCancellationTokenSource.Token);
                }
            }
            catch(Exception e)
            {
                Fault(e);
            }
        }

        private async Task ReceiveMessageAsync(AwaitableSocketAsyncEventArgs socketEvent)
        {
            try
            {
                socketEvent.SetAnyEndPoint(socket.AddressFamily);

                if(socket.ReceiveFromAsync(socketEvent))
                {
                    await socketEvent.WaitUntilCompletedAsync(processingCancellationTokenSource.Token);
                }
                else
                {
                    socketEvent.ThrowIfNotSuccess();
                }

                var message = socketEvent.GetDatagram();

                await receivingBuffer.SendAsync(message, processingCancellationTokenSource.Token);

                ReleaseSocketEvent(socketEvent);
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
            processingCancellationTokenSource.Cancel();
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
            processingCancellationTokenSource.Cancel();
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
                    await socketEvent.WaitUntilCompletedAsync(processingCancellationTokenSource.Token);
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
