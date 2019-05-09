using System;
using System.Collections.Immutable;
using System.Net;

namespace Datagrammer
{
    public sealed class Datagram
    {
        private readonly byte[] bytes;
        private readonly byte[] address;
        private readonly int port;

        public Datagram(byte[] bytes, IPEndPoint endPoint)
        {
            this.bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));

            if(endPoint == null)
            {
                throw new ArgumentNullException(nameof(endPoint));
            }
            
            address = endPoint.Address.GetAddressBytes();
            port = endPoint.Port;
        }

        public IPEndPoint GetEndPoint()
        {
            return new IPEndPoint(new IPAddress(address), port);
        }

        public byte[] GetBytes()
        {
            return (byte[])bytes.Clone();
        }

        public ReadOnlyMemory<byte> AsMemory()
        {
            return new ReadOnlyMemory<byte>(bytes);
        }

        public ImmutableArray<byte> AsImmutable()
        {
            return ImmutableArray.Create(bytes);
        }
    }
}
