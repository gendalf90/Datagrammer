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
        public void Complete_IsCompleted()
        {
            //Arrange
            //Act
            var block = DatagramBlock.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });

            block.Complete();

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
        }

        [Fact]
        public void Cancel_IsCanceled()
        {
            //Arrange
            var source = new CancellationTokenSource();

            //Act
            var block = DatagramBlock.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
                opt.CancellationToken = source.Token;
            });

            source.Cancel();

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }

        [Fact]
        public void Fault_IsCompletedWithError()
        {
            //Arrange
            //Act
            var block = DatagramBlock.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });

            block.Fault(new ApplicationException());

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<ApplicationException>();
        }

        [Fact]
        public async Task DisposeSocket_IsCompletedWithError()
        {
            //Arrange
            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            //Act
            var block = DatagramBlock.Start(opt =>
            {
                opt.Socket = socket;
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });

            await Task.Delay(1000);

            socket.Dispose();

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<SocketException>();
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
            var receivedMessages = new List<Try<Datagram>>();

            //Act
            var block = DatagramBlock.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
                opt.ReceivingBufferCapacity = toSendMessages.Count;
            });
            var sendingTasks = toSendMessages.Select(message => block.SendAsync(new Try<Datagram>(message)));

            await Task.WhenAll(sendingTasks);
            await Task.Delay(1000);

            block.Complete();

            while (await block.OutputAvailableAsync())
            {
                receivedMessages.Add(block.Receive());
            }

            //Assert
            block
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
            receivedMessages
                .Select(message => message.Value.Buffer.ToArray())
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
            var block = DatagramBlock.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
                opt.ReceivingBufferCapacity = toSendMessages.Count;
            });
            var sendingTasks = toSendMessages.Select(message => block.SendAsync(new Try<Datagram>(message)));

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
            var block = DatagramBlock.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
                opt.ReceivingBufferCapacity = 10;
                opt.SendingBufferCapacity = 10;
                opt.CancellationToken = cancellationSource.Token;
            });

            for (byte i = 0; i < 15; i++)
            {
                await block.SendAsync(new Try<Datagram>(loopbackDatagram.WithBuffer(new byte[] { i, i, i })));
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