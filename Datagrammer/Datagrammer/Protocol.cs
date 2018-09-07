using System.Net.Sockets;
using System.Threading.Tasks;

namespace Datagrammer
{
    internal class Protocol : IProtocol
    {
        private readonly UdpClient udpClient;

        public Protocol(UdpClient udpClient)
        {
            this.udpClient = udpClient;
        }

        public async Task<Datagram> ReceiveAsync()
        {
            var data = await udpClient.ReceiveAsync();

            return new Datagram
            {
                EndPoint = data.RemoteEndPoint,
                Bytes = data.Buffer
            };
        }

        public async Task SendAsync(Datagram data)
        {
            await udpClient.SendAsync(data.Bytes, data.Bytes.Length, data.EndPoint);
        }

        public void Dispose()
        {
            udpClient.Close();
            udpClient.Dispose();
        }
    }
}
