using Datagrammer;
using FluentAssertions;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Xunit;
using System;
using Datagrammer.Channels;

namespace Tests.UseCases
{
    public class DatagrammerUsing
    {
        [Fact(DisplayName = "simple starting and completion")]
        public async Task CaseOne()
        {
            var datagrammer = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = new IPEndPoint(IPAddress.Loopback, 50021)
            });

            datagrammer.Start();

            await datagrammer.Initialization;

            datagrammer.Complete();

            await datagrammer.Completion;
        }

        [Fact(DisplayName = "simple sending and receiving")]
        public async Task CaseTwo()
        {
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, 50022);
            var receivedBytes = new ConcurrentBag<byte[]>();

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = loopbackEndPoint
            });

            datagramBlock.Start();

            await datagramBlock.Initialization;

            for(byte i = 0; i < 3; i++)
            {
                await datagramBlock.SendAsync(new Datagram(new byte[] { i, i, i }, loopbackEndPoint));
            }

            for(byte i = 0; i < 3; i++)
            {
                var message = await datagramBlock.ReceiveAsync();

                receivedBytes.Add(message.Buffer.ToArray());
            }

            datagramBlock.Complete();

            await datagramBlock.Completion;

            receivedBytes.Should().BeEquivalentTo(new[]
            {
                new byte[] { 0, 0, 0 },
                new byte[] { 1, 1, 1 },
                new byte[] { 2, 2, 2 }
            });
        }

        [Fact(DisplayName = "simple sending and receiving with dataflow way")]
        public async Task CaseThree()
        {
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, 50023);
            var receivedBytes = new ConcurrentBag<byte[]>();

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = loopbackEndPoint
            });

            var targetBlock = new ActionBlock<Datagram>(datagram =>
            {
                receivedBytes.Add(datagram.Buffer.ToArray());
            });

            datagramBlock.LinkTo(targetBlock);

            datagramBlock.Start();

            await datagramBlock.Initialization;

            for (byte i = 0; i < 3; i++)
            {
                await datagramBlock.SendAsync(new Datagram(new byte[] { i, i, i }, loopbackEndPoint));
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            datagramBlock.Complete();

            await datagramBlock.Completion;

            receivedBytes.Should().BeEquivalentTo(new[]
            {
                new byte[] { 0, 0, 0 },
                new byte[] { 1, 1, 1 },
                new byte[] { 2, 2, 2 }
            });
        }

        [Fact(DisplayName = "simple sending and receiving with reactive way")]
        public async Task CaseFour()
        {
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, 50024);
            var receivedBytes = new BlockingCollection<byte[]>();

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = loopbackEndPoint
            });

            var observer = datagramBlock.AsObserver();

            //It is more convenient with the Reactive Extensions using
            datagramBlock.AsObservable().Subscribe(message =>
            {
                receivedBytes.Add(message.Buffer.ToArray());
            }, 
            () =>
            {
                receivedBytes.CompleteAdding();
            });

            datagramBlock.Start();

            await datagramBlock.Initialization;

            for (byte i = 0; i < 3; i++)
            {
                observer.OnNext(new Datagram(new byte[] { i, i, i }, loopbackEndPoint));
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            observer.OnCompleted();

            while (!receivedBytes.IsAddingCompleted);

            datagramBlock.Completion.IsCompleted.Should().BeTrue();

            receivedBytes.Should().BeEquivalentTo(new[]
            {
                new byte[] { 0, 0, 0 },
                new byte[] { 1, 1, 1 },
                new byte[] { 2, 2, 2 }
            });
        }

        [Fact(DisplayName = "simple sending and receiving with channel way")]
        public async Task CaseFive()
        {
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, 50025);
            var receivedBytes = new ConcurrentBag<byte[]>();

            var datagramBlock = new DatagramBlock(new DatagramOptions
            {
                ListeningPoint = loopbackEndPoint
            });

            var channel = datagramBlock.AsChannel();

            datagramBlock.Start();

            await datagramBlock.Initialization;

            for (byte i = 0; i < 3; i++)
            {
                await channel.Writer.WriteAsync(new Datagram(new byte[] { i, i, i }, loopbackEndPoint));
            }

            for (byte i = 0; i < 3; i++)
            {
                var message = await channel.Reader.ReadAsync();

                receivedBytes.Add(message.Buffer.ToArray());
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            channel.Writer.Complete();

            await channel.Reader.Completion;

            datagramBlock.Completion.IsCompleted.Should().BeTrue();

            receivedBytes.Should().BeEquivalentTo(new[]
            {
                new byte[] { 0, 0, 0 },
                new byte[] { 1, 1, 1 },
                new byte[] { 2, 2, 2 }
            });
        }
    }
}
