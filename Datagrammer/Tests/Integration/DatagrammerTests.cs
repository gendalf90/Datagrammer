using Datagrammer;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using System.Linq;

namespace Tests.Integration
{
    public class DatagrammerTests
    {
        private readonly IPEndPoint sendingEndPoint = new IPEndPoint(IPAddress.Loopback, 50001);
        private readonly IPEndPoint receivingEndPoint = new IPEndPoint(IPAddress.Loopback, 50002);
        private readonly TimeSpan delayTime = TimeSpan.FromSeconds(3);

        [Fact]
        public async Task StartingAndFinishing()
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
    }
}
