using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Channels
{
    public sealed class ChannelAdapter : Channel<Datagram>
    {
        private readonly IPropagatorBlock<Datagram, Datagram> datagramBlock;
        private readonly Channel<Datagram> inputChannel;
        private readonly Channel<Datagram> outputChannel;
        private readonly CancellationTokenSource processingCancellationTokenSource;

        public ChannelAdapter(IPropagatorBlock<Datagram, Datagram> datagramBlock) : this(datagramBlock, new ChannelOptions())
        {
        }

        public ChannelAdapter(IPropagatorBlock<Datagram, Datagram> datagramBlock, ChannelOptions options)
        {
            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.datagramBlock = datagramBlock ?? throw new ArgumentNullException(nameof(datagramBlock));

            inputChannel = Channel.CreateBounded<Datagram>(new BoundedChannelOptions(options.InputBufferCapacity)
            {
                SingleReader = true,
                AllowSynchronousContinuations = true
            });

            outputChannel = Channel.CreateBounded<Datagram>(new BoundedChannelOptions(options.OutputBufferCapacity)
            {
                SingleWriter = true,
                AllowSynchronousContinuations = true
            });

            Writer = inputChannel.Writer;
            Reader = outputChannel.Reader;
            
            processingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);

            Task.Factory.StartNew(StartProcessing, options.CancellationToken, TaskCreationOptions.None, options.TaskScheduler);
        }

        private void StartProcessing()
        {
            StartInputAsync();
            StartOutputAsync();
            CompleteAsync();
        }

        private async void StartInputAsync()
        {
            try
            {
                while (true)
                {
                    var datagram = await inputChannel.Reader.ReadAsync(processingCancellationTokenSource.Token);
                    await datagramBlock.SendAsync(datagram, processingCancellationTokenSource.Token);
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private async void StartOutputAsync()
        {
            try
            {
                while (true)
                {
                    var datagram = await datagramBlock.ReceiveAsync(processingCancellationTokenSource.Token);
                    await outputChannel.Writer.WriteAsync(datagram, processingCancellationTokenSource.Token);
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private async void CompleteAsync()
        {
            try
            {
                await Task.WhenAny(datagramBlock.Completion, inputChannel.Reader.Completion);

                Complete();
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void Complete()
        {
            inputChannel.Writer.TryComplete();
            outputChannel.Writer.TryComplete();
            datagramBlock.Complete();
            processingCancellationTokenSource.Cancel();
        }

        private void Fault(Exception e)
        {
            inputChannel.Writer.TryComplete(e);
            outputChannel.Writer.TryComplete(e);
            datagramBlock.Fault(e);
            processingCancellationTokenSource.Cancel();
        }
    }
}
