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

namespace Tests.Integration
{
    public class DatagrammerTests
    {
        private readonly IPEndPoint sendingEndPoint = new IPEndPoint(IPAddress.Loopback, 50001);
        private readonly IPEndPoint receivingEndPoint = new IPEndPoint(IPAddress.Loopback, 50002);
        private readonly TimeSpan delayTime = TimeSpan.FromSeconds(3);

        [Fact]
        public void StartingAndFinishing()
        {
            var datagramBlock = new DatagramBlock();

            datagramBlock.Start();
            datagramBlock.Complete();

            datagramBlock.Invoking(block => block.Start())
                         .Should()
                         .NotThrow();
            datagramBlock.Invoking(block => block.Complete())
                         .Should()
                         .NotThrow();
            datagramBlock.Awaiting(block => block.Initialization)
                         .Should()
                         .NotThrow();
            datagramBlock.Awaiting(block => block.Completion)
                         .Should()
                         .NotThrow();
        }

        [Fact]
        public async Task SendingAndReceiving()
        {
            var sendingBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = sendingEndPoint
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

            receivedMessages.Select(message => message.Buffer.ToArray())
                            .Should()
                            .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
        }

        [Fact]
        public async Task ParallelSendingAndReceiving()
        {
            var sendingBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = sendingEndPoint,
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

            receivedMessages.Select(message => message.Buffer.ToArray())
                            .Should()
                            .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
        }

        [Fact]
        public async Task UseBoundSocket()
        {
            using (var socket = new Socket(SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(receivingEndPoint);

                var sendingBlock = new DatagramBlock();
                var toSendMessages = new List<Datagram>();

                for (int i = 0; i < 100; i++)
                {
                    toSendMessages.Add(new Datagram(BitConverter.GetBytes(i), receivingEndPoint));
                }

                var receivingBlock = new DatagramBlock(new DatagramOptions
                {
                    Socket = socket,
                    DisposeSocketAfterCompletion = false
                });
                var receivedMessages = new List<Datagram>();

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

                receivedMessages.Select(message => message.Buffer.ToArray())
                                .Should()
                                .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
            }
        }

        [Fact]
        public async Task Disposing()
        {
            var cancellation = new CancellationTokenSource();

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                CancellationToken = cancellation.Token
            });

            datagramBlock.Start();

            await datagramBlock.Initialization;

            cancellation.Cancel();

            datagramBlock
                .Awaiting(block => block.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }

        [Fact]
        public async Task SocketErrorsHandling()
        {
            var socketErrors = new List<SocketException>();

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                SocketErrorHandler = e =>
                {
                    socketErrors.Add(e);

                    return Task.CompletedTask;
                }
            });

            var toSendMessages = new List<Datagram>()
            {
                new Datagram(new byte[] { 1, 2, 3 }, receivingEndPoint),
                new Datagram(new byte[100000], receivingEndPoint),
                new Datagram(new byte[] { 1, 2, 3 }, new byte[1000], 0)
            };

            datagramBlock.Start();

            await datagramBlock.Initialization;

            var sendingTasks = toSendMessages.Select(datagramBlock.SendAsync);

            await Task.WhenAll(sendingTasks);
            await Task.Delay(delayTime);

            datagramBlock.Complete();

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
            var loopbackBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = receivingEndPoint
            });
            var toSendMessages = new List<Datagram>
            {
                new Datagram( new byte[] { 1, 2, 3 }, receivingEndPoint),
                new Datagram( new byte[] { 4, 5, 6 }, receivingEndPoint),
                new Datagram( new byte[] { 7, 8, 9 }, receivingEndPoint),
                new Datagram( new byte[] { 10, 11, 12 }, receivingEndPoint),
                new Datagram( new byte[] { 13, 14, 15 }, receivingEndPoint)
            };
            var receivedMessages = new List<Datagram>();

            loopbackBlock.Start();
            await loopbackBlock.Initialization;

            var channel = loopbackBlock.AsChannel();

            var readingTask = Task.Run(async () =>
            {
                for(int i = 0; i < toSendMessages.Count; i++)
                {
                    var datagram = await channel.Reader.ReadAsync();

                    receivedMessages.Add(datagram);
                }
            });

            foreach(var datagram in toSendMessages)
            {
                await channel.Writer.WriteAsync(datagram);
            }

            await readingTask;

            channel.Writer.Complete();

            await channel.Reader.Completion;

            loopbackBlock.Complete();

            await loopbackBlock.Completion;

            receivedMessages.Select(message => message.Buffer.ToArray())
                            .Should()
                            .BeEquivalentTo(toSendMessages.Select(message => message.Buffer.ToArray()));
        }
    }
}
