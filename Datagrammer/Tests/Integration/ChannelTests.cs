using Datagrammer;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;

namespace Tests.Integration
{
    public class ChannelTests
    {
        [Fact]
        public void Complete_BeforeStart_IsCompleted()
        {
            //Arrange
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });

            channel.Writer.Complete();

            //Assert
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
        }

        [Fact]
        public void Fault_BeforeStart_IsCompletedWithError()
        {
            //Arrange
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });

            channel.Writer.Complete(new ApplicationException());

            //Assert
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<ApplicationException>();
        }

        [Fact]
        public void Cancel_BeforeStart_IsCanceled()
        {
            //Arrange
            var source = new CancellationTokenSource();

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
                opt.CancellationToken = source.Token;
            });

            source.Cancel();

            //Assert
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }

        [Fact]
        public void Complete_AfterStart_IsCompleted()
        {
            //Arrange
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });

            channel.Writer.Complete();

            //Assert
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
        }

        [Fact]
        public void Fault_AfterStart_IsCompletedWithError()
        {
            //Arrange
            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });

            channel.Writer.Complete(new ApplicationException());

            //Assert
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<ApplicationException>();
        }

        [Fact]
        public void Cancel_AfterStart_IsCanceled()
        {
            //Arrange
            var source = new CancellationTokenSource();

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
                opt.CancellationToken = source.Token;
            });

            source.Cancel();

            //Assert
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }

        [Fact]
        public void BindSocket_SocketIsAlreadyBound_DoNotBindIt()
        {
            //Arrange
            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            var endPointToBind = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var listeningEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());

            //Act
            socket.Bind(endPointToBind);

            var channel = DatagramChannel.Start(opt =>
            {
                opt.Socket = socket;
                opt.ListeningPoint = listeningEndPoint;
                opt.DisposeSocket = false;
            });

            channel.Writer.Complete();

            //Assert
            socket.IsBound.Should().BeTrue();
            socket.LocalEndPoint.As<IPEndPoint>().Address.MapToIPv6().Should().Be(endPointToBind.Address.MapToIPv6());
            socket.LocalEndPoint.As<IPEndPoint>().Port.Should().Be(endPointToBind.Port);
        }

        [Fact]
        public void BindSocket_SocketIsNotBound_BindIt()
        {
            //Arrange
            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            var listeningEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.Socket = socket;
                opt.ListeningPoint = listeningEndPoint;
                opt.DisposeSocket = false;
            });

            channel.Writer.Complete();

            //Assert
            socket.IsBound.Should().BeTrue();
            socket.LocalEndPoint.As<IPEndPoint>().Address.MapToIPv6().Should().Be(listeningEndPoint.Address.MapToIPv6());
            socket.LocalEndPoint.As<IPEndPoint>().Port.Should().Be(listeningEndPoint.Port);
        }

        [Fact]
        public void DisposeSocket_ItIsNotNeededToDisposeSocket_SocketIsNotDisposed()
        {
            //Arrange
            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.Socket = socket;
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
                opt.DisposeSocket = false;
            });

            channel.Writer.Complete();

            //Assert
            socket
                .Invoking(s => s.LocalEndPoint)
                .Should()
                .NotThrow();
        }

        [Fact]
        public async Task DisposeSocket_ItIsNeededToDisposeSocket_SocketIsDisposed()
        {
            //Arrange
            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.Socket = socket;
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
                opt.DisposeSocket = true;
            });

            channel.Writer.Complete();

            await channel.Reader.Completion;
            await Task.Delay(1000);

            //Assert
            socket
                .Invoking(s => s.LocalEndPoint)
                .Should()
                .Throw<ObjectDisposedException>();
        }

        [Fact]
        public void Start_SocketIsAlreadyDisposed_ErrorWhileStarting()
        {
            //Arrange
            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            //Act
            socket.Dispose();

            //Assert
            Assert.Throws<ObjectDisposedException>(() => DatagramChannel.Start(opt =>
            {
                opt.Socket = socket;
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            }));
        }

        [Fact]
        public async Task LoopbackSendingAndReceiving()
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
                opt.SendingBufferCapacity = toSendMessages.Count;
                opt.ReceivingBufferCapacity = toSendMessages.Count;
            });

            foreach (var message in toSendMessages)
            {
                await channel.Writer.WriteAsync(message);
            }

            await Task.Delay(1000);

            channel.Writer.Complete();

            await foreach(var message in channel.Reader.ReadAllAsync())
            {
                receivedMessages.Add(message);
            }

            await channel.Reader.Completion;

            //Assert
            receivedMessages.Select(message => message.Buffer.ToArray())
                            .Should()
                            .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
        }

        [Fact]
        public async Task SocketErrorsHandling()
        {
            //Arrange
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var toSendMessages = new List<Datagram>()
            {
                new Datagram(new byte[] { 1, 2, 3 }, loopbackEndPoint.Address.GetAddressBytes(), -10),
                new Datagram(new byte[100000], loopbackEndPoint.Address.GetAddressBytes(), loopbackEndPoint.Port),
                new Datagram(new byte[] { 1, 2, 3 }, new byte[1000], 50000)
            };
            var socketErrors = new ConcurrentBag<SocketException>();

            //Act
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
                opt.DisposeSocket = false;
                opt.ErrorHandler = (e) =>
                {
                    socketErrors.Add(e);

                    return Task.CompletedTask;
                };
            });

            foreach (var message in toSendMessages)
            {
                await channel.Writer.WriteAsync(message);
            }

            channel.Writer.Complete();

            //Assert
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();

            socketErrors
                .Should()
                .HaveCountGreaterOrEqualTo(3);
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
                opt.SendingBufferCapacity = 10;
                opt.ReceivingBufferCapacity = 10;
                opt.CancellationToken = cancellationSource.Token;
            });

            for (int i = 0; i < 15; i++)
            {
                await channel.Writer.WriteAsync(loopbackDatagram.WithBuffer(BitConverter.GetBytes(i)));
            }

            cancellationSource.Cancel();

            while (!channel.Reader.Completion.IsCompleted)
            {
                channel.Reader.TryRead(out var message);
            }

            //Assert
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }
    }
}
