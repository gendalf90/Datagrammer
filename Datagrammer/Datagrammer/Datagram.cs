using System;
using System.Net;
using System.Threading;

namespace Datagrammer
{
    public readonly struct Datagram
    {
        private readonly IPAddress cachedIpAddress;

        public Datagram(ReadOnlyMemory<byte> buffer, ReadOnlyMemory<byte> address, int port)
        {
            Buffer = buffer;
            Address = address;
            Port = port;
            cachedIpAddress = null;
        }

        public Datagram(byte[] buffer, IPEndPoint endPoint)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

            if (endPoint == null)
            {
                throw new ArgumentNullException(nameof(endPoint));
            }
            
            Address = endPoint.Address.GetAddressBytes();
            Port = endPoint.Port;
            cachedIpAddress = endPoint.Address;
        }

        public Datagram(byte[] buffer, string address, int port)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

            var parsedIpAddress = IPAddress.Parse(address);

            Address = parsedIpAddress.GetAddressBytes();
            Port = port;
            cachedIpAddress = parsedIpAddress;
        }

        public ReadOnlyMemory<byte> Buffer { get; }

        public ReadOnlyMemory<byte> Address { get; }

        public int Port { get; }

        internal IPAddress GetIPAddress()
        {
            return cachedIpAddress == null ? new IPAddress(Address.Span) : cachedIpAddress;
        }
    }
}
