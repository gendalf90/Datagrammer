using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Datagrammer.Channels
{
    public sealed class ChannelAdapter<T> : Channel<T>
    {
        private readonly IPropagatorBlock<T, T> datagramBlock;
        private readonly Channel<T> inputChannel;
        private readonly Channel<T> outputChannel;
        private readonly ChannelOptions options;

        public ChannelAdapter(IPropagatorBlock<T, T> datagramBlock) : this(datagramBlock, new ChannelOptions())
        {
        }

        public ChannelAdapter(IPropagatorBlock<T, T> datagramBlock, ChannelOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            this.datagramBlock = datagramBlock ?? throw new ArgumentNullException(nameof(datagramBlock));

            inputChannel = Channel.CreateBounded<T>(new BoundedChannelOptions(options.InputBufferCapacity)
            {
                SingleReader = true,
                AllowSynchronousContinuations = true
            });

            outputChannel = Channel.CreateBounded<T>(new BoundedChannelOptions(options.OutputBufferCapacity)
            {
                SingleWriter = true,
                AllowSynchronousContinuations = true
            });

            Writer = inputChannel.Writer;
            Reader = outputChannel.Reader;

            StartProcessing();
        }

        private void StartProcessing()
        {
            Task.Factory.StartNew(StartInputAsync , CancellationToken.None, TaskCreationOptions.None, options.TaskScheduler);
            Task.Factory.StartNew(StartOutputAsync, CancellationToken.None, TaskCreationOptions.None, options.TaskScheduler);
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
