using System;
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
        private readonly ITargetBlock<object> receivingAction;
        private readonly CancellationTokenSource receivingCancellationTokenSource;
        private readonly TaskCompletionSource<int> initializationTaskSource;
        private readonly Socket socket;
        private readonly SocketEventPool socketEventPool;

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

            receivingAction = new ActionBlock<object>(ReceiveMessageAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.ReceivingParallelismDegree,
                MaxDegreeOfParallelism = options.ReceivingParallelismDegree,
                CancellationToken = options.CancellationToken,
                TaskScheduler = options.TaskScheduler,
                SingleProducerConstrained = true
            });

            socketEventPool = new SocketEventPool();
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
            sendingBuffer.Fault(e);
            receivingAction.Fault(e);
            sendingAction.Fault(e);
            receivingBuffer.Fault(e);
            receivingCancellationTokenSource.Cancel();
            initializationTaskSource.SetException(e);
        }

        private void StartProcessing()
        {
            LinkSendingAction();
            StartAsyncActions(
                CompleteReceivingBufferAsync,
                DisposeSocketIfNeededAsync,
                ProcessMessageReceivingAsync,
                DisposeSocketEventPoolAsync);
        }

        private void StartAsyncActions(params Func<Task>[] asyncActions)
        {
            foreach(var action in asyncActions)
            {
                Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, options.TaskScheduler);
            }
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

        private async Task DisposeSocketEventPoolAsync()
        {
            using (socketEventPool)
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
            await receivingAction.SendAsync(null, receivingCancellationTokenSource.Token);
        }

        private async Task ReceiveMessageAsync(object state)
        {
            var socketEvent = socketEventPool.GetOrCreate();

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

                socketEvent.Reset();

                await receivingBuffer.SendAsync(message, options.CancellationToken);
            }
            catch(SocketException e)
            {
                socketEvent.Reset();

                await HandleSocketErrorAsync(e);
            }
            catch(Exception e)
            {
                Fault(e);
            }
            finally
            {
                socketEventPool.Release(socketEvent);
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
            var socketEvent = socketEventPool.GetOrCreate();

            try
            {
                socketEvent.SetDatagram(message);

                if (socket.SendToAsync(socketEvent))
                {
                    await socketEvent.WaitUntilCompletedAsync(options.CancellationToken);
                }
                else
                {
                    socketEvent.ThrowIfNotSuccess();
                }

                socketEvent.Reset();
            }
            catch (SocketException e)
            {
                socketEvent.Reset();

                await HandleSocketErrorAsync(e);
            }
            catch (Exception e)
            {
                Fault(e);
            }
            finally
            {
                socketEventPool.Release(socketEvent);
            }
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
