using Datagrammer;
using Datagrammer.AsyncEnumerables;
using FluentAssertions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tests.Integration
{
    public class AsyncEnumerableTests
    {
        private readonly Socket socket;
        private readonly IPEndPoint listeningEndPoint;
        private readonly IPEndPoint sendingEndPoint;
        private readonly IAsyncEnumerable<AsyncEnumeratorContext> input;
        private readonly IAsyncEnumerable<AsyncEnumeratorContext> output;

        public AsyncEnumerableTests()
        {
            sendingEndPoint = new IPEndPoint(IPAddress.Loopback, TestNetwork.GetNextPort());
            listeningEndPoint = new IPEndPoint(IPAddress.Any, TestNetwork.GetNextPort());
            socket = DatagramSocket
                .Create()
                .Listen(listeningEndPoint);
            input = socket.ToInputEnumerable();
            output = socket.ToOutputEnumerable();
        }

        [Fact]
        public async Task ReceivePackets()
        {
            //Arrange
            var packets = TestNetwork.GeneratePackets(10);
            var results = new BlockingCollection<byte[]>(10);

            //Act
            var receivingTask = Task.Run(async () =>
            {
                await foreach (var context in output)
                {
                    results.Add(context.Buffer.ToArray());

                    if (results.Count == results.BoundedCapacity)
                    {
                        break;
                    }
                }
            });

            var sendingTask = TestNetwork.SendPacketsTo(listeningEndPoint.Port, packets);

            await Task.WhenAll(receivingTask, sendingTask);

            //Assert
            results.Should().BeEquivalentTo(packets);
        }
    }
}
