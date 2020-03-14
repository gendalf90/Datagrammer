using Datagrammer;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Datagrammer.Channels;
using System.Collections.Concurrent;

namespace Tests.Integration
{
    public class DatagrammerTests
    {
        private readonly TimeSpan delayTime = TimeSpan.FromSeconds(3);

        [Fact]
        public void Initialization_WithDefaultOptions_Success()
        {
            //Arrange
            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
            datagramBlock.Start();
            datagramBlock.Complete();

            //Assert
            datagramBlock.Awaiting(block => block.Initialization)
                         .Should()
                         .NotThrow();
        }

        [Fact]
        public void ErrorWhileInitialization_SocketIsDisposed_InitializationAndCompletionThrowError()
        {
            //Arrange
            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            socket.Dispose();

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                Socket = socket,
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
            datagramBlock.Start();

            //Assert
            datagramBlock.Awaiting(block => block.Initialization)
                         .Should()
                         .Throw<ObjectDisposedException>();
            datagramBlock.Awaiting(block => block.Completion)
                         .Should()
                         .Throw<ObjectDisposedException>();
        }

        [Fact]
        public async Task DisposeSocketAfterCompletion_DisposeFlagIsTrue_SocketIsDisposed()
        {
            //Arrange
            var testSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                Socket = testSocket,
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext()),
                DisposeSocketAfterCompletion = true
            });

            //Act
            datagramBlock.Start();
            datagramBlock.Complete();

            await datagramBlock.Completion;

            //Assert
            testSocket
                .Invoking(socket => socket.Bind(new IPEndPoint(IPAddress.Loopback, 50003)))
                .Should()
                .Throw<ObjectDisposedException>();
        }

        [Fact]
        public async Task DisposeSocketAfterCompletion_DisposeFlagIsFalse_SocketIsNotDisposed()
        {
            //Arrange
            var testSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                Socket = testSocket,
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext()),
                DisposeSocketAfterCompletion = false
            });

            //Act
            datagramBlock.Start();
            datagramBlock.Complete();

            await datagramBlock.Completion;

            //Assert
            testSocket
                .Invoking(socket => socket.Bind(new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())))
                .Should()
                .NotThrow<ObjectDisposedException>();
        }

        [Fact]
        public void Completion_WithDefaultOptions_Success()
        {
            //Arrange
            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
            datagramBlock.Start();
            datagramBlock.Complete();

            //Assert
            datagramBlock.Awaiting(block => block.Completion)
                         .Should()
                         .NotThrow();
        }

        [Fact]
        public void Completion_AfterFault_ThrowError()
        {
            //Arrange
            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
            datagramBlock.Start();
            datagramBlock.Fault(new Exception());

            //Assert
            datagramBlock.Awaiting(block => block.Completion)
                         .Should()
                         .Throw<Exception>();
        }

        [Fact]
        public async Task SendingAndReceiving()
        {
            //Arrange
            var receivingEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var sendingBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });
            var toSendMessages = new List<Datagram>
            {
                new Datagram( new byte[] { 1, 2, 3 }, receivingEndPoint),
                new Datagram( new byte[] { 4, 5, 6 }, receivingEndPoint),
                new Datagram( new byte[] { 7, 8, 9 }, receivingEndPoint),
                new Datagram( new byte[] { 10, 11, 12 }, receivingEndPoint),
                new Datagram( new byte[] { 13, 14, 15 }, receivingEndPoint)
            };
            var receivingBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = receivingEndPoint
            });
            var receivedMessages = new List<Datagram>();

            //Act
            using (receivingBlock.AsObservable().Subscribe(message => receivedMessages.Add(message)))
            {
                receivingBlock.Start();
                sendingBlock.Start();
                await Task.WhenAll(sendingBlock.Initialization, receivingBlock.Initialization);
                var sendingTasks = toSendMessages.Select(sendingBlock.SendAsync);
                await Task.WhenAll(sendingTasks);
                await Task.Delay(delayTime);
                receivingBlock.Complete();
                sendingBlock.Complete();
                await Task.WhenAll(sendingBlock.Completion, receivingBlock.Completion);
            }

            //Assert
            receivedMessages.Select(message => message.Buffer.ToArray())
                            .Should()
                            .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
        }

        [Fact]
        public async Task ParallelSendingAndReceiving()
        {
            //Arrange
            var receivingEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var sendingBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext()),
                SendingBufferCapacity = 10,
                SendingParallelismDegree = 4
            });
            var toSendMessages = new List<Datagram>();

            for(int i = 0; i < 1000; i++)
            {
                toSendMessages.Add(new Datagram(BitConverter.GetBytes(i), receivingEndPoint));
            }

            var receivingBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = receivingEndPoint,
                ReceivingBufferCapacity = 10,
                ReceivingParallelismDegree = 4
            });
            var receivedMessages = new List<Datagram>();

            //Act
            using (receivingBlock.AsObservable().Subscribe(message => receivedMessages.Add(message)))
            {
                receivingBlock.Start();
                sendingBlock.Start();
                await Task.WhenAll(sendingBlock.Initialization, receivingBlock.Initialization);
                var sendingTasks = toSendMessages.Select(sendingBlock.SendAsync);
                await Task.WhenAll(sendingTasks);
                await Task.Delay(delayTime);
                receivingBlock.Complete();
                sendingBlock.Complete();
                await Task.WhenAll(sendingBlock.Completion, receivingBlock.Completion);
            }

            //Assert
            receivedMessages.Select(message => message.Buffer.ToArray())
                            .Should()
                            .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
        }

        [Fact]
        public void SocketIsAlreadyBound_DoNotBindItTwice()
        {
            //Arrange
            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            var endPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());

            socket.Bind(endPoint);

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
            datagramBlock.Start();
            datagramBlock.Complete();

            //Assert
            datagramBlock
                .Awaiting(block => block.Initialization)
                .Should()
                .NotThrow();
            socket.IsBound.Should().BeTrue();
        }

        [Fact]
        public void Cancellation_CancelBeforeStarting_InitializationShouldNotThrowErrorAndCompletionThrowError()
        {
            //Arrange
            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                CancellationToken = new CancellationToken(true),
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
            datagramBlock.Start();

            //Assert
            datagramBlock
                .Awaiting(block => block.Initialization)
                .Should()
                .NotThrow();
            datagramBlock
                .Awaiting(block => block.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }

        [Fact]
        public async Task Cancellation_CancelAfterInitialization_InitializationShouldNotThrowErrorAndCompletionThrowError()
        {
            //Arrange
            var cancellationSource = new CancellationTokenSource();

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                CancellationToken = cancellationSource.Token,
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });

            //Act
            datagramBlock.Start();

            await datagramBlock.Initialization;

            cancellationSource.Cancel();

            //Assert
            datagramBlock
                .Awaiting(block => block.Initialization)
                .Should()
                .NotThrow();
            datagramBlock
                .Awaiting(block => block.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }

        [Fact]
        public async Task SocketErrorsHandling()
        {
            //Arrange
            var endPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var socketErrors = new ConcurrentBag<SocketException>();

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = endPoint,
                SocketErrorHandler = e =>
                {
                    socketErrors.Add(e);

                    return Task.CompletedTask;
                }
            });

            var toSendMessages = new List<Datagram>()
            {
                new Datagram(new byte[] { 1, 2, 3 }, new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())),
                new Datagram(new byte[100000], endPoint),
                new Datagram(new byte[] { 1, 2, 3 }, new byte[1000], 0)
            };

            //Act
            datagramBlock.Start();

            await datagramBlock.Initialization;

            var sendingTasks = toSendMessages.Select(datagramBlock.SendAsync);

            await Task.WhenAll(sendingTasks);
            await Task.Delay(delayTime);

            datagramBlock.Complete();

            //Assert
            datagramBlock.Awaiting(block => block.Completion)
                         .Should()
                         .NotThrow();
            socketErrors
                .Should()
                .HaveCount(3)
                .And
                .AllBeOfType<SocketException>();
        }

        [Fact]
        public async Task UseChannelAdapter()
        {
            //Arrange
            var endPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var loopbackBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = endPoint
            });
            var toSendMessages = new List<Datagram>
            {
                new Datagram( new byte[] { 1, 2, 3 }, endPoint),
                new Datagram( new byte[] { 4, 5, 6 }, endPoint),
                new Datagram( new byte[] { 7, 8, 9 }, endPoint),
                new Datagram( new byte[] { 10, 11, 12 }, endPoint),
                new Datagram( new byte[] { 13, 14, 15 }, endPoint)
            };
            var receivedMessages = new ConcurrentBag<Datagram>();

            //Act
            loopbackBlock.Start();
            await loopbackBlock.Initialization;

            var channel = loopbackBlock.AsChannel();

            var readingTask = Task.Run(async () =>
            {
                await foreach(var message in channel.Reader.ReadAllAsync())
                {
                    receivedMessages.Add(message);
                }
            });

            foreach(var datagram in toSendMessages)
            {
                await channel.Writer.WriteAsync(datagram);
            }

            await Task.Delay(delayTime);

            channel.Writer.Complete();

            await Task.WhenAll(channel.Reader.Completion, readingTask);

            loopbackBlock.Complete();

            await loopbackBlock.Completion;

            //Assert
            receivedMessages.Select(message => message.Buffer.ToArray())
                            .Should()
                            .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
        }

        [Fact]
        public async Task ConsumeMessagesFromBufferAfterCompletion()
        {
            //Arrange
            var receivingEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var sendingBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext())
            });
            var toSendMessages = new List<Datagram>();

            for (int i = 0; i < 10; i++)
            {
                toSendMessages.Add(new Datagram(BitConverter.GetBytes(i), receivingEndPoint));
            }

            var receivingBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = receivingEndPoint,
                ReceivingBufferCapacity = toSendMessages.Count
            });
            var receivedMessages = new List<Datagram>();

            //Act
            receivingBlock.Start();
            sendingBlock.Start();
            await Task.WhenAll(sendingBlock.Initialization, receivingBlock.Initialization);
            var sendingTasks = toSendMessages.Select(sendingBlock.SendAsync);
            await Task.WhenAll(sendingTasks);
            await Task.Delay(delayTime);
            sendingBlock.Complete();
            receivingBlock.Complete();

            for (int i = 0; i < toSendMessages.Count; i++)
            {
                receivedMessages.Add(await receivingBlock.ReceiveAsync());
            }

            await Task.WhenAll(sendingBlock.Completion, receivingBlock.Completion);

            //Assert
            receivedMessages.Select(message => message.Buffer.ToArray())
                            .Should()
                            .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
        }

        [Fact]
        public async Task CancelWhileSending_SuccessfulCancellation()
        {
            //Arrange
            var cancellationSource = new CancellationTokenSource();
            var receivingEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var sendingBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext()),
                CancellationToken = cancellationSource.Token
            });
            var receivingBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = receivingEndPoint,
                CancellationToken = cancellationSource.Token
            });
            receivingBlock.LinkTo(DataflowBlock.NullTarget<Datagram>());

            //Act
            receivingBlock.Start();
            sendingBlock.Start();
            await Task.WhenAll(sendingBlock.Initialization, receivingBlock.Initialization);

            for (int i = 0; i < 5; i++)
            {
                await sendingBlock.SendAsync(new Datagram(BitConverter.GetBytes(i), receivingEndPoint));
            }

            cancellationSource.Cancel();
            await Task.Delay(delayTime);

            //Assert
            sendingBlock
                .Awaiting(block => block.Completion)
                .Should()
                .Throw<OperationCanceledException>();
            receivingBlock
                .Awaiting(block => block.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }
    }
}
