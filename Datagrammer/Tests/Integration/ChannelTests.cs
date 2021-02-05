//using Datagrammer;
//using System;
//using System.Net;
//using System.Threading.Tasks;
//using Xunit;
//using FluentAssertions;
//using System.Collections.Generic;
//using System.Net.Sockets;
//using System.Linq;
//using Datagrammer.Channels;

//namespace Tests.Integration
//{
//    public class ChannelTests
//    {
//        [Fact]
//        public void Complete_IsCompleted()
//        {
//            //Arrange
//            var channel = DatagramChannel.Start(opt =>
//            {
//                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
//            });

//            //Act
//            channel.Writer.Complete();

//            //Assert
//            channel.Reader
//                .Awaiting(reader => reader.Completion)
//                .Should()
//                .NotThrow();
//        }

//        [Fact]
//        public void Fault_IsCompletedWithError()
//        {
//            //Arrange
//            var channel = DatagramChannel.Start(opt =>
//            {
//                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
//            });

//            //Act
//            channel.Writer.Complete(new ApplicationException());

//            //Assert
//            channel.Reader
//                .Awaiting(reader => reader.Completion)
//                .Should()
//                .Throw<ApplicationException>();
//        }

//        [Fact]
//        public async Task Complete_SocketIsDisposedAfterCompletion()
//        {
//            //Arrange
//            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
//            var channel = DatagramChannel.Start(opt =>
//            {
//                opt.Socket = socket;
//                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
//            });

//            //Act
//            channel.Writer.Complete();

//            await channel.Reader.Completion;

//            //Assert
//            socket
//                .Invoking(s => s.LocalEndPoint)
//                .Should()
//                .Throw<ObjectDisposedException>();
//        }

//        [Fact]
//        public async Task DisposeSocketAfterStart_CompleteWithError()
//        {
//            //Arrange
//            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
//            var channel = DatagramChannel.Start(opt =>
//            {
//                opt.Socket = socket;
//                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
//            });

//            //Act
//            await Task.Delay(1000);

//            socket.Dispose();

//            //Assert
//            channel.Reader
//                .Awaiting(reader => reader.Completion)
//                .Should()
//                .Throw<SocketException>();
//        }

//        [Fact]
//        public async Task Complete_WhileReceiving_ReceiveAllMessages()
//        {
//            //Arrange
//            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
//            var loopbackDatagram = new Datagram().WithEndPoint(loopbackEndPoint);
//            var toSendMessages = new List<Datagram>
//            {
//                loopbackDatagram.WithBuffer(new byte[] { 1, 2, 3 }),
//                loopbackDatagram.WithBuffer(new byte[] { 4, 5, 6 }),
//                loopbackDatagram.WithBuffer(new byte[] { 7, 8, 9 }),
//                loopbackDatagram.WithBuffer(new byte[] { 10, 11, 12 }),
//                loopbackDatagram.WithBuffer(new byte[] { 13, 14, 15 })
//            };
//            var receivedMessages = new List<Datagram>();
//            var channel = DatagramChannel.Start(opt =>
//            {
//                opt.ListeningPoint = loopbackEndPoint;
//                opt.ReceivingBufferCapacity = toSendMessages.Count;
//            });

//            //Act
//            foreach (var message in toSendMessages)
//            {
//                await channel.Writer.WriteAsync(new Try<Datagram>(message));
//            }

//            await Task.Delay(1000);

//            channel.Writer.Complete();

//            await foreach (var message in channel.Reader.ReadAllAsync())
//            {
//                receivedMessages.Add(message.Value);
//            }

//            //Assert
//            channel.Reader
//                .Awaiting(reader => reader.Completion)
//                .Should()
//                .NotThrow();
//            receivedMessages
//                .Select(message => message.Buffer.ToArray())
//                .Should()
//                .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
//        }

//        [Fact]
//        public async Task Fault_WhileReceiving_ReceiveAllMessages()
//        {
//            //Arrange
//            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
//            var loopbackDatagram = new Datagram().WithEndPoint(loopbackEndPoint);
//            var toSendMessages = new List<Datagram>
//            {
//                loopbackDatagram.WithBuffer(new byte[] { 1, 2, 3 }),
//                loopbackDatagram.WithBuffer(new byte[] { 4, 5, 6 }),
//                loopbackDatagram.WithBuffer(new byte[] { 7, 8, 9 }),
//                loopbackDatagram.WithBuffer(new byte[] { 10, 11, 12 }),
//                loopbackDatagram.WithBuffer(new byte[] { 13, 14, 15 })
//            };
//            var receivedMessages = new List<Datagram>();
//            var channel = DatagramChannel.Start(opt =>
//            {
//                opt.ListeningPoint = loopbackEndPoint;
//                opt.ReceivingBufferCapacity = toSendMessages.Count;
//            });

//            //Act
//            foreach (var message in toSendMessages)
//            {
//                await channel.Writer.WriteAsync(new Try<Datagram>(message));
//            }

//            await Task.Delay(1000);

//            channel.Writer.Complete(new ApplicationException());

//            while (channel.Reader.TryRead(out var message))
//            {
//                receivedMessages.Add(message.Value);
//            }

//            //Assert
//            channel.Reader
//                .Awaiting(reader => reader.Completion)
//                .Should()
//                .Throw<ApplicationException>();
//            receivedMessages
//                .Select(message => message.Buffer.ToArray())
//                .Should()
//                .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
//        }

//        [Fact]
//        public async Task SocketErrorsHandling()
//        {
//            //Arrange
//            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
//            var toSendMessages = new List<Datagram>()
//            {
//                new Datagram(new byte[] { 1, 2, 3 }, loopbackEndPoint.Address.GetAddressBytes(), -10),
//                new Datagram(new byte[100000], loopbackEndPoint.Address.GetAddressBytes(), loopbackEndPoint.Port),
//                new Datagram(new byte[] { 1, 2, 3 }, new byte[1000], 50000)
//            };
//            var receivedMessages = new List<Try<Datagram>>();
//            var channel = DatagramChannel.Start(opt =>
//            {
//                opt.ListeningPoint = loopbackEndPoint;
//                opt.ReceivingBufferCapacity = 3;
//            });

//            //Act
//            foreach (var message in toSendMessages)
//            {
//                await channel.Writer.WriteAsync(new Try<Datagram>(message));
//            }

//            await Task.Delay(1000);

//            channel.Writer.Complete();

//            while (channel.Reader.TryRead(out var message))
//            {
//                receivedMessages.Add(message);
//            }

//            //Assert
//            channel.Reader
//                .Awaiting(reader => reader.Completion)
//                .Should()
//                .NotThrow();

//            receivedMessages
//                .Should()
//                .HaveCount(3)
//                .And
//                .OnlyContain(message => message.IsException());
//        }

//        [Fact]
//        public async Task ErrorWhileSending_CompletionWithError()
//        {
//            //Arrange
//            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
//            var loopbackDatagram = new Datagram().WithEndPoint(loopbackEndPoint);
//            var channel = DatagramChannel.Start(opt =>
//            {
//                opt.ListeningPoint = loopbackEndPoint;
//                opt.SendingBufferCapacity = 10;
//                opt.ReceivingBufferCapacity = 10;
//            });

//            //Act
//            for (int i = 0; i < 15; i++)
//            {
//                await channel.Writer.WriteAsync(new Try<Datagram>(loopbackDatagram.WithBuffer(BitConverter.GetBytes(i))));
//            }

//            channel.Writer.Complete(new OperationCanceledException());

//            while (!channel.Reader.Completion.IsCompleted)
//            {
//                channel.Reader.TryRead(out var message);
//            }

//            //Assert
//            channel.Reader
//                .Awaiting(reader => reader.Completion)
//                .Should()
//                .Throw<OperationCanceledException>();
//        }
//    }
//}
