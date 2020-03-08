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

            Task.Factory.StartNew(StartProcessing, options.CancellationToken, TaskCreationOptions.None, options.TaskScheduler);
        }

        private void StartProcessing()
        {
            StartInputAsync();
            StartOutputAsync();
            CompleteDatagramBlockAsync();
            CompleteChannelAsync();
        }

        private async void StartInputAsync()
        {
            try
            {
                while (true)
                {
                    var datagram = await inputChannel.Reader.ReadAsync(options.CancellationToken);
                    await datagramBlock.SendAsync(datagram, options.CancellationToken);
                }
            }
            catch (OperationCanceledException e)
            {
                FaultAll(e);
            }
        }

        private async void StartOutputAsync()
        {
            try
            {
                while (true)
                {
                    var datagram = await datagramBlock.ReceiveAsync(options.CancellationToken);
                    await outputChannel.Writer.WriteAsync(datagram, options.CancellationToken);
                }
            }
            catch (OperationCanceledException e)
            {
                FaultAll(e);
            }
        }

        private async void CompleteDatagramBlockAsync()
        {
            try
            {
                await datagramBlock.Completion;

                CompleteAll();
            }
            catch (Exception e)
            {
                FaultAll(e);
            }
        }

        private async void CompleteChannelAsync()
        {
            try
            {
                await inputChannel.Reader.Completion;

                datagramBlock.Complete();
            }
            catch(Exception e)
            {
                datagramBlock.Fault(e);
            }

            try
            {
                await datagramBlock.Completion;

                outputChannel.Writer.Complete();
            }
            catch(Exception e)
            {
                outputChannel.Writer.Complete(e);
            }
        }

        private void CompleteAll()
        {
            inputChannel.Writer.TryComplete();
            outputChannel.Writer.TryComplete();
            datagramBlock.Complete();
        }

        private void FaultAll(Exception e)
        {
            inputChannel.Writer.TryComplete(e);
            outputChannel.Writer.TryComplete(e);
            datagramBlock.Fault(e);
        }
    }
}
