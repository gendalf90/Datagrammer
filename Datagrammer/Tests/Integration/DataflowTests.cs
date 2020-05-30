using Datagrammer;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Datagrammer.Dataflow;
using System.Threading.Tasks.Dataflow;

namespace Tests.Integration
{
    public class DataflowTests
    {
        [Fact]
        public void Complete_DoNotCompleteChannel_IsCompleted()
        {
            //Arrange
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });
            var block = channel.ToDataflowBlock();

            block.Complete();

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
            channel.Reader.Completion.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void Fault_DoNotCompleteChannel_IsCompletedWithError()
        {
            //Arrange
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });
            var block = channel.ToDataflowBlock();

            block.Fault(new ApplicationException());

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<ApplicationException>();
            channel.Reader.Completion.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void Cancel_DoNotCompleteChannel_IsCanceled()
        {
            //Arrange
            var source = new CancellationTokenSource();

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });
            var block = channel.ToDataflowBlock(opt =>
            {
                opt.CancellationToken = source.Token;
            });

            source.Cancel();

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<OperationCanceledException>();
            channel.Reader.Completion.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void Complete_WithChannel_IsCompleted()
        {
            //Arrange
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });
            var block = channel.ToDataflowBlock(opt =>
            {
                opt.CompleteChannel = true;
            });

            block.Complete();

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
        }

        [Fact]
        public void Fault_WithChannel_IsCompletedWithError()
        {
            //Arrange
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });
            var block = channel.ToDataflowBlock(opt =>
            {
                opt.CompleteChannel = true;
            });

            block.Fault(new ApplicationException());

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<ApplicationException>();
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<ApplicationException>();
        }

        [Fact]
        public void Cancel_WithChannel_IsCanceled()
        {
            //Arrange
            var source = new CancellationTokenSource();

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });
            var block = channel.ToDataflowBlock(opt =>
            {
                opt.CancellationToken = source.Token;
                opt.CompleteChannel = true;
            });

            source.Cancel();

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<OperationCanceledException>();
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }

        [Fact]
        public void Complete_ByChannel_IsCompleted()
        {
            //Arrange
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });
            var block = channel.ToDataflowBlock();

            channel.Writer.Complete();

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
        }

        [Fact]
        public void Fault_ByChannel_IsCompletedWithError()
        {
            //Arrange
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });
            var block = channel.ToDataflowBlock();

            channel.Writer.Complete(new ApplicationException());

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<ApplicationException>();
        }

        [Fact]
        public void Cancel_ByChannel_IsCanceled()
        {
            //Arrange
            var source = new CancellationTokenSource();

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
                opt.CancellationToken = source.Token;
            });
            var block = channel.ToDataflowBlock();

            source.Cancel();

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }

        [Fact]
        public async Task Complete_WhileReceiving_ReceiveAllMessages()
        {
            //Arrange
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var loopbackDatagram = new Datagram().WithEndPoint(loopbackEndPoint);
            var toSendMessages = new List<Datagram>
            {
                loopbackDatagram.WithBuffer(new byte[] { 1, 2, 3 }),
                loopbackDatagram.WithBuffer(new byte[] { 4, 5, 6 }),
                loopbackDatagram.WithBuffer(new byte[] { 7, 8, 9 }),
                loopbackDatagram.WithBuffer(new byte[] { 10, 11, 12 }),
                loopbackDatagram.WithBuffer(new byte[] { 13, 14, 15 })
            };
            var receivedMessages = new List<Datagram>();
            
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
            });
            var block = channel.ToDataflowBlock(opt =>
            {
                opt.ReceivingBufferCapacity = toSendMessages.Count;
            });
            var sendingTasks = toSendMessages.Select(block.SendAsync);

            await Task.WhenAll(sendingTasks);
            await Task.Delay(1000);

            block.Complete();

            while(await block.OutputAvailableAsync())
            {
                receivedMessages.Add(block.Receive());
            }

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
            receivedMessages
                .Select(message => message.Buffer.ToArray())
                .Should()
                .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
        }

        [Fact]
        public async Task Fault_WhileReceiving_ErrorWhenReceiving()
        {
            //Arrange
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var loopbackDatagram = new Datagram().WithEndPoint(loopbackEndPoint);
            var toSendMessages = new List<Datagram>
            {
                loopbackDatagram.WithBuffer(new byte[] { 1, 2, 3 }),
                loopbackDatagram.WithBuffer(new byte[] { 4, 5, 6 }),
                loopbackDatagram.WithBuffer(new byte[] { 7, 8, 9 }),
                loopbackDatagram.WithBuffer(new byte[] { 10, 11, 12 }),
                loopbackDatagram.WithBuffer(new byte[] { 13, 14, 15 })
            };

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
            });
            var block = channel.ToDataflowBlock(opt =>
            {
                opt.ReceivingBufferCapacity = toSendMessages.Count;
            });
            var sendingTasks = toSendMessages.Select(block.SendAsync);

            await Task.WhenAll(sendingTasks);
            await Task.Delay(1000);

            block.Fault(new ApplicationException());

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<ApplicationException>();
            block
                .Awaiting(reader => reader.ReceiveAsync())
                .Should()
                .Throw<InvalidOperationException>();
        }

        [Fact]
        public async Task CancelWhileSending_SuccessfulCancellation()
        {
            //Arrange
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var loopbackDatagram = new Datagram().WithEndPoint(loopbackEndPoint);
            var cancellationSource = new CancellationTokenSource();

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
            });
            var block = channel.ToDataflowBlock(opt =>
            {
                opt.ReceivingBufferCapacity = 10;
                opt.SendingBufferCapacity = 10;
                opt.CancellationToken = cancellationSource.Token;
            });

            for (byte i = 0; i < 15; i++)
            {
                await block.SendAsync(loopbackDatagram.WithBuffer(new byte[] { i, i, i }));
            }

            cancellationSource.Cancel();

            //Assert
            block
                .Awaiting(b => b.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }

        [Fact]
        public async Task ReceiveFromChannel_WithMultipleBlocks_MessagesAreNotLost()
        {
            //Arrange
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var loopbackDatagram = new Datagram().WithEndPoint(loopbackEndPoint);
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
            });
            var block1 = channel.ToDataflowBlock(opt =>
            {
                opt.ReceivingBufferCapacity = 50;
            });
            var block2 = channel.ToDataflowBlock(opt =>
            {
                opt.ReceivingBufferCapacity = 50;
            });
            var block3 = channel.ToDataflowBlock(opt =>
            {
                opt.ReceivingBufferCapacity = 50;
            });
            var resultBlock = new BufferBlock<Datagram>(new DataflowBlockOptions
            {
                BoundedCapacity = 100
            });

            //Act
            block1.LinkTo(resultBlock);
            block2.LinkTo(resultBlock);
            block3.LinkTo(resultBlock);

            for (byte i = 0; i < 100; i++)
            {
                await channel.Writer.WriteAsync(loopbackDatagram.WithBuffer(new byte[] { i, i, i }));
            }

            block1.Complete();

            await Task.Delay(500);

            block2.Complete();

            await Task.Delay(500);

            block3.Complete();

            await Task.WhenAll(block1.Completion, block2.Completion, block3.Completion);

            resultBlock.TryReceiveAll(out var receivedMessages);

            //Assert
            receivedMessages.Count.Should().Be(100);
        }
    }
}