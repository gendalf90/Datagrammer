using Datagrammer;
using FluentAssertions;
using System.Net;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Xunit;
using System;
using Datagrammer.Dataflow;
using System.Collections.Generic;
using Datagrammer.Channels;

namespace Tests.UseCases
{
    public class DatagrammerUsing
    {
        [Fact(DisplayName = "simple starting and completion with channel way")]
        public async Task CaseOne()
        {
            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });

            channel.Writer.Complete();

            await channel.Reader.Completion;
        }

        [Fact(DisplayName = "simple starting and completion with dataflow way")]
        public async Task CaseTwo()
        {
            var dataflowBlock = DatagramBlock.Start(opt =>
            {
                opt.ListeningPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            });

            dataflowBlock.Complete();

            await dataflowBlock.Completion;
        }

        [Fact(DisplayName = "simple sending and receiving with channel way")]
        public async Task CaseThree()
        {
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var loopbackDatagram = new Datagram().WithEndPoint(loopbackEndPoint);
            var receivedBytes = new List<byte[]>();

            var channel = DatagramChannel.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
                opt.ReceivingBufferCapacity = 3;
                opt.SendingBufferCapacity = 3;
            });

            for (byte i = 0; i < 3; i++)
            {
                await channel.Writer.WriteAsync(loopbackDatagram.WithBuffer(new byte[] { i, i, i }).AsTry());
            }

            for (byte i = 0; i < 3; i++)
            {
                var message = await channel.Reader.ReadAsync();

                receivedBytes.Add(message.Value.Buffer.ToArray());
            }

            channel.Writer.Complete();

            await channel.Reader.Completion;

            receivedBytes.Should().BeEquivalentTo(new[]
            {
                new byte[] { 0, 0, 0 },
                new byte[] { 1, 1, 1 },
                new byte[] { 2, 2, 2 }
            });
        }

        [Fact(DisplayName = "simple sending and receiving with dataflow way")]
        public async Task CaseFour()
        {
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var loopbackDatagram = new Datagram().WithEndPoint(loopbackEndPoint);
            var receivedBytes = new List<byte[]>();

            var dataflowBlock = DatagramBlock.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
                opt.ReceivingBufferCapacity = 3;
                opt.SendingBufferCapacity = 3;
            });

            for (byte i = 0; i < 3; i++)
            {
                await dataflowBlock.SendAsync(loopbackDatagram.WithBuffer(new byte[] { i, i, i }).AsTry());
            }

            for (byte i = 0; i < 3; i++)
            {
                var message = await dataflowBlock.ReceiveAsync();

                receivedBytes.Add(message.Value.Buffer.ToArray());
            }

            dataflowBlock.Complete();

            await dataflowBlock.Completion;

            receivedBytes.Should().BeEquivalentTo(new[]
            {
                new byte[] { 0, 0, 0 },
                new byte[] { 1, 1, 1 },
                new byte[] { 2, 2, 2 }
            });
        }

        [Fact(DisplayName = "simple sending and receiving with reactive way")]
        public async Task CaseFive()
        {
            var loopbackEndPoint = new IPEndPoint(IPAddress.Loopback, TestPort.GetNext());
            var loopbackDatagram = new Datagram().WithEndPoint(loopbackEndPoint);
            var receivedBytes = new List<byte[]>();

            var dataflowBlock = DatagramBlock.Start(opt =>
            {
                opt.ListeningPoint = loopbackEndPoint;
            });
            var observer = dataflowBlock.AsObserver();

            //It is more convenient with the Reactive Extensions using
            dataflowBlock.AsObservable().Subscribe(message =>
            {
                receivedBytes.Add(message.Value.Buffer.ToArray());
            });

            for (byte i = 0; i < 3; i++)
            {
                observer.OnNext(loopbackDatagram.WithBuffer(new byte[] { i, i, i }).AsTry());
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            observer.OnCompleted();

            await dataflowBlock.Completion;

            receivedBytes.Should().BeEquivalentTo(new[]
            {
                new byte[] { 0, 0, 0 },
                new byte[] { 1, 1, 1 },
                new byte[] { 2, 2, 2 }
            });
        }
    }
}
