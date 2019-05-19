using System;
using System.Buffers;
using System.Net;
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
        private readonly TaskCompletionSource<int> initializationTaskSource;

        private UdpClient udpClient;
        private int state = NotInitializedState;

        public DatagramBlock() : this(new DatagramOptions())
        {
        }

        public DatagramBlock(DatagramOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            initializationTaskSource = new TaskCompletionSource<int>();

            sendingBuffer = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = options.SendingBufferCapacity
            });

            receivingBuffer = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = options.ReceivingBufferCapacity
            });

            sendingAction = new ActionBlock<Datagram>(SendMessageAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.SendingParallelismDegree,
                MaxDegreeOfParallelism = options.SendingParallelismDegree
            });

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
            udpClient = new UdpClient(options.ListeningPoint);
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
            ReceiveMessagesSafeAsync();
        }

        private void LinkSendingAction()
        {
            sendingBuffer.LinkTo(sendingAction);
        }

        private async void ReceiveMessagesSafeAsync()
        {
            try
            {
                await ReceiveMessagesAsync();
            }
            catch(Exception e)
            {
                Fault(e);
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            while (true)
            {
                try
                {
                    var message = await ReceiveMessageAsync();
                    await ProcessMessageAsync(message);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (ProcessingStoppedException)
                {
                    break;
                }
            };
        }

        private async Task<Datagram> ReceiveMessageAsync()
        {
            var data = await udpClient.ReceiveAsync();
            return new Datagram(data.Buffer, data.RemoteEndPoint);
        }

        private async Task ProcessMessageAsync(Datagram message)
        {
            var isMessageReceived = await receivingBuffer.SendAsync(message);

            if(!isMessageReceived)
            {
                throw new ProcessingStoppedException();
            }
        }

        public Task Completion { get; private set; }

        private async Task CompleteAsync()
        {
            using (udpClient)
            {
                await Task.WhenAll(initializationTaskSource.Task,
                                   sendingBuffer.Completion,
                                   sendingAction.Completion,
                                   receivingBuffer.Completion);
            }
        }

        public void Complete()
        {
            receivingBuffer.Complete();
            sendingBuffer.Complete();
            sendingAction.Complete();
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
        }

        private async Task SendMessageAsync(Datagram message)
        {
            var sendingBuffer = ArrayPool<byte>.Shared.Rent(message.Buffer.Length);
            
            try
            {
                message.Buffer.CopyTo(sendingBuffer);
                var endPoint = new IPEndPoint(new IPAddress(message.Address.Span), message.Port);
                await udpClient.SendAsync(sendingBuffer, message.Buffer.Length, endPoint);
            }
            catch(Exception e)
            {
                Fault(e);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sendingBuffer);
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
