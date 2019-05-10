using System;
using System.Net;

namespace Datagrammer
{
    public readonly struct Datagram
    {
        public Datagram(ReadOnlyMemory<byte> buffer, ReadOnlyMemory<byte> address, int port)
        {
            Buffer = buffer;
            Address = address;
            Port = port;
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
        }

        public ReadOnlyMemory<byte> Buffer { get; }

        public ReadOnlyMemory<byte> Address { get; }

        public int Port { get; }
    }
}
