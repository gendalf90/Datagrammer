using System.Net.Sockets;

namespace Datagrammer
{
    public static class DatagramSocketFactory
    {
        public static Socket Create(bool useIPv6 = false)
        {
            return new Socket(
                useIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork,
                SocketType.Dgram, 
                ProtocolType.Udp);
        }
    }
}
