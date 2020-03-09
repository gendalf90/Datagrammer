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
        private readonly ChannelOptions options;

        public ChannelAdapter(IPropagatorBlock<Datagram, Datagram> datagramBlock) : this(datagramBlock, new ChannelOptions())
        {
        }

        public ChannelAdapter(IPropagatorBlock<Datagram, Datagram> datagramBlock, ChannelOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            this.datagramBlock = datagramBlock ?? throw new ArgumentNullException(nameof(datagramBlock));

            inputChannel = Channel.CreateBounded<Datagram>(new BoundedChannelOptions(options.InputBufferCapacity)
            {
                SingleReader = true
            });

            outputChannel = Channel.CreateBounded<Datagram>(new BoundedChannelOptions(options.OutputBufferCapacity)
            {
                SingleWriter = true
            });

            Writer = inputChannel.Writer;
            Reader = outputChannel.Reader;

            StartProcessing();
        }

        private void StartProcessing()
        {
            Task.Factory.StartNew(() =>
            {
                Task.Factory.StartNew(StartInputAsync);
                Task.Factory.StartNew(StartOutputAsync);
            }, CancellationToken.None, TaskCreationOptions.None, options.TaskScheduler);
        }

        private async Task StartInputAsync()
        {
            try
            {
                while (true)
                {
                    await PerformInputAsync();
                }
            }
            catch (ChannelClosedException)
            {
                await CompleteDatagramBlockAsync();
                await CompleteOutputChannelAsync();
            }
            catch (Exception e)
            {
                FaultAll(e);
            }
        }

        private async Task PerformInputAsync()
        {
            var datagram = await inputChannel.Reader.ReadAsync(options.CancellationToken);

            await datagramBlock.SendAsync(datagram, options.CancellationToken);
        }

        private async Task StartOutputAsync()
        {
            try
            {
                while (true)
                {
                    await PerformOutputAsync();
                }
            }
            catch (InvalidOperationException)
            {
                await CompleteInputChannelAsync();
                await CompleteOutputChannelAsync();
            }
            catch (Exception e)
            {
                FaultAll(e);
            }
        }

        private async Task PerformOutputAsync()
        {
            var datagram = await datagramBlock.ReceiveAsync(options.CancellationToken);

            await outputChannel.Writer.WriteAsync(datagram, options.CancellationToken);
        }

        private async Task CompleteDatagramBlockAsync()
        {
            try
            {
                await inputChannel.Reader.Completion;

                datagramBlock.Complete();
            }
            catch (Exception e)
            {
                datagramBlock.Fault(e);
            }
        }

        private async Task CompleteOutputChannelAsync()
        {
            try
            {
                await datagramBlock.Completion;

                outputChannel.Writer.TryComplete();
            }
            catch (Exception e)
            {
                outputChannel.Writer.TryComplete(e);
            }
        }

        private async Task CompleteInputChannelAsync()
        {
            try
            {
                await datagramBlock.Completion;

                inputChannel.Writer.TryComplete();
            }
            catch (Exception e)
            {
                inputChannel.Writer.TryComplete(e);
            }
        }

        private void FaultAll(Exception e)
        {
            inputChannel.Writer.TryComplete(e);
            outputChannel.Writer.TryComplete(e);
            datagramBlock.Fault(e);
        }
    }
}
