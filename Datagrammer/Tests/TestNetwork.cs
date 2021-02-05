using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    public static class TestNetwork
    {
        private static int initialPort = 50000;
        private static ThreadLocal<Random> random = new ThreadLocal<Random>(() => new Random());

        public static int GetNextPort()
        {
            return Interlocked.Increment(ref initialPort);
        }

        public static IEnumerable<byte[]> GeneratePackets(int count)
        {
            var packets = new byte[count][];

            for (int i = 0; i < count; i++)
            {
                packets[i] = new byte[random.Value.Next(1, 1024)];

                random.Value.NextBytes(packets[i]);
            }

            return packets;
        }

        public static async Task SendPacketsTo(int port, IEnumerable<byte[]> packets)
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, port);
            var client = new UdpClient(GetNextPort());

            foreach (var packet in packets)
            {
                await Task.Delay(TimeSpan.FromSeconds(random.Value.NextDouble()));

                await client.SendAsync(packet, packet.Length, endPoint);
            }
        }

        public static async Task<IEnumerable<byte[]>> ReceivePacketsFrom(int port, int count)
        {
            var client = new UdpClient(port);
            var packets = new byte[count][];

            for (int i = 0; i < count; i++)
            {
               var result = await client.ReceiveAsync();

                packets[i] = result.Buffer;
            }

            return packets;
        }
    }
}
