using Datagrammer;
using FluentAssertions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tests.Integration
{
    public class AsyncEnumerableTests
    {
        [Fact]
        public async Task ReceivePackets()
        {
            //Arrange
            var packets = TestNetwork.GeneratePackets(10);
            var results = new BlockingCollection<byte[]>(10);
            using var socket = DatagramSocketFactory.Create();
            var port = TestNetwork.GetNextPort();

            //Act
            socket.Bind(new IPEndPoint(IPAddress.Any, port));

            var receivingTask = Task.Run(async () =>
            {
                await foreach (var context in socket.ToOutputEnumerable())
                {
                    results.Add(context.Buffer.AsMemory(context.Offset, context.Length).ToArray());

                    if (results.Count == results.BoundedCapacity)
                    {
                        break;
                    }
                }
            });

            var sendingTask = TestNetwork.SendPacketsTo(port, packets);

            await Task.WhenAll(receivingTask, sendingTask);

            //Assert
            results.Should().BeEquivalentTo(packets);
        }

        [Fact]
        public async Task ReceivePackets_WithCancellation()
        {
            //Arrange
            var packets = TestNetwork.GeneratePackets(1);
            var results = new BlockingCollection<byte[]>(1);
            using var socket = DatagramSocketFactory.Create();
            var port = TestNetwork.GetNextPort();
            var cancellationSource = new CancellationTokenSource();
            var random = new Random();

            //Act
            socket.Bind(new IPEndPoint(IPAddress.Any, port));

            var receivingTask = Task.Run(async () =>
            {
                await foreach (var context in socket.ToOutputEnumerable().WithCancellation(cancellationSource.Token))
                {
                    results.Add(context.Buffer.AsMemory(context.Offset, context.Length).ToArray());

                    if (results.Count == results.BoundedCapacity)
                    {
                        cancellationSource.CancelAfter(TimeSpan.FromSeconds(random.NextDouble()));
                    }
                }
            });

            await TestNetwork.SendPacketsTo(port, packets);

            //Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => receivingTask);
        }
    }
}
