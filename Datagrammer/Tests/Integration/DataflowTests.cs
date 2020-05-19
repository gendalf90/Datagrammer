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
        public async Task SendingAndReceiving()
        {
            //Arrange
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var toSendMessages = new List<Datagram>
            {
                new Datagram( new byte[] { 1, 2, 3 }, loopbackEndPoint.Address.GetAddressBytes(), loopbackEndPoint.Port),
                new Datagram( new byte[] { 4, 5, 6 }, loopbackEndPoint.Address.GetAddressBytes(), loopbackEndPoint.Port),
                new Datagram( new byte[] { 7, 8, 9 }, loopbackEndPoint.Address.GetAddressBytes(), loopbackEndPoint.Port),
                new Datagram( new byte[] { 10, 11, 12 }, loopbackEndPoint.Address.GetAddressBytes(), loopbackEndPoint.Port),
                new Datagram( new byte[] { 13, 14, 15 }, loopbackEndPoint.Address.GetAddressBytes(), loopbackEndPoint.Port)
            };
            var receivedMessages = new List<Datagram>();
            
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
            });
            var block = channel.ToDataflowBlock();

            using (block.AsObservable().Subscribe(receivedMessages.Add))
            {
                var sendingTasks = toSendMessages.Select(block.SendAsync);
                await Task.WhenAll(sendingTasks);
                await Task.Delay(1000);
                block.Complete();
                await block.Completion;
            }

            //Assert
            receivedMessages.Select(message => message.Buffer.ToArray())
                            .Should()
                            .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
        }

        [Fact]
        public async Task CancelWhileSending_SuccessfulCancellation()
        {
            //Arrange
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
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

            for (int i = 0; i < 15; i++)
            {
                await block.SendAsync(new Datagram(BitConverter.GetBytes(i), loopbackEndPoint.Address.GetAddressBytes(), loopbackEndPoint.Port));
            }

            cancellationSource.Cancel();

            //Assert
            block
                .Awaiting(b => b.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }
    }
}