using System.Net;
using System.Net.Sockets;

namespace Datagrammer
{
    internal class ProtocolCreator : IProtocolCreator
    {
        public IProtocol Create(IPEndPoint listeningPoint)
        {
            var udpClient = new UdpClient(listeningPoint);
            return new Protocol(udpClient);
        }
    }
}
