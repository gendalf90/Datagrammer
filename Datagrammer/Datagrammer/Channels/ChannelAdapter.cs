using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Channels
{
    public sealed class ChannelAdapter : Channel<Datagram>
    {
        private readonly ChannelAdapterOptions options;
        private readonly IPropagatorBlock<Datagram, Datagram> datagramBlock;
        private readonly Channel<Datagram> inputChannel;
        private readonly Channel<Datagram> outputChannel;
        private readonly CancellationTokenSource processingCancellationTokenSource;
        private readonly TaskScheduler taskScheduler;

        public ChannelAdapter(IPropagatorBlock<Datagram, Datagram> datagramBlock) : this(datagramBlock, new ChannelAdapterOptions())
        {
        }

        public ChannelAdapter(IPropagatorBlock<Datagram, Datagram> datagramBlock, ChannelAdapterOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.datagramBlock = datagramBlock ?? throw new ArgumentNullException(nameof(datagramBlock));

            taskScheduler = options.TaskScheduler ?? throw new ArgumentNullException(nameof(options.TaskScheduler));

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

            Task.Factory.StartNew(StartProcessing, options.CancellationToken, TaskCreationOptions.None, taskScheduler);
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
                Complete(e);
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
                Complete(e);
            }
        }

        private async void CompleteAsync()
        {
            Exception exception = null;

            try
            {
                await Task.WhenAny(datagramBlock.Completion, inputChannel.Reader.Completion);
            }
            catch (Exception e)
            {
                exception = e;
            }
            finally
            {
                Complete(exception);
            }
        }

        private void Complete(Exception e)
        {
            inputChannel.Writer.TryComplete(e);
            outputChannel.Writer.TryComplete(e);
            processingCancellationTokenSource.Cancel();
        }
    }
}
