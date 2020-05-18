﻿using Datagrammer;
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext()),
                CancellationToken = source.Token
            });

            //Act
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
            channel.Start();
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
            channel.Start();
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext()),
                CancellationToken = source.Token
            });

            //Act
            channel.Start();
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

            socket.Bind(endPointToBind);

            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                Socket = socket,
                ListeningPoint = listeningEndPoint,
                DisposeSocket = false
            });

            //Act
            channel.Start();
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                Socket = socket,
                ListeningPoint = listeningEndPoint,
                DisposeSocket = false
            });

            //Act
            channel.Start();
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                Socket = socket,
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext()),
                DisposeSocket = false
            });

            //Act
            channel.Start();
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                Socket = socket,
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext()),
                DisposeSocket = true
            });

            //Act
            channel.Start();
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
        public void Start_SocketIsAlreadyDisposed_ChannelIsCompletedWithError()
        {
            //Arrange
            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            socket.Dispose();

            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                Socket = socket,
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
            channel.Start();

            //Assert
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<ObjectDisposedException>();
        }

        [Fact]
        public async Task LoopbackSendingAndReceiving()
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                ListeningPoint = loopbackEndPoint,
                SendingBufferCapacity = toSendMessages.Count,
                ReceivingBufferCapacity = toSendMessages.Count
            });
            var receivedMessages = new List<Datagram>();

            //Act
            channel.Start();

            foreach(var message in toSendMessages)
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
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                ListeningPoint = loopbackEndPoint,
                DisposeSocket = false
            });

            channel.SocketErrorHandler += (o, e) =>
            {
                socketErrors.Add(e.Error);
            };

            //Act
            channel.Start();

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
                .HaveCount(3)
                .And
                .AllBeOfType<SocketException>();
        }

        [Fact]
        public async Task CancelWhileSending_SuccessfulCancellation()
        {
            //Arrange
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var cancellationSource = new CancellationTokenSource();
            var channel = new DatagramChannel(new DatagramChannelOptions
            {
                ListeningPoint = loopbackEndPoint,
                SendingBufferCapacity = 10,
                ReceivingBufferCapacity = 10,
                CancellationToken = cancellationSource.Token
            });

            //Act
            channel.Start();

            for (int i = 0; i < 15; i++)
            {
                await channel.Writer.WriteAsync(new Datagram(BitConverter.GetBytes(i), loopbackEndPoint.Address.GetAddressBytes(), loopbackEndPoint.Port));
            }

            await Task.Delay(1000);

            cancellationSource.Cancel();

            while (channel.Reader.TryRead(out var message)) ;

            //Assert
            channel.Reader
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }
    }
}
