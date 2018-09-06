using System.Net;
using System.Net.Sockets;

namespace Datagrammer
{
    internal class MessageClientCreator : IMessageClientCreator
    {
        public IMessageClient Create(IPEndPoint listeningPoint)
        {
            var udpClient = new UdpClient(listeningPoint);
            return new MessageClient(udpClient);
        }
    }
}
