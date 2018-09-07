using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace Datagrammer
{
    internal class ProtocolCreator : IProtocolCreator
    {
        private readonly IOptions<DatagramOptions> options;

        public ProtocolCreator(IOptions<DatagramOptions> options)
        {
            this.options = options;
        }

        public IProtocol Create()
        {
            var udpClient = new UdpClient(options.Value.ListeningPoint);
            return new Protocol(udpClient);
        }
    }
}
