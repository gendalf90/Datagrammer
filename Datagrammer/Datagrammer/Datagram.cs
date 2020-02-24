using System;
using System.Net;

namespace Datagrammer
{
    public readonly struct Datagram : IEquatable<Datagram>
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

        public bool Equals(Datagram other)
        {
            return Buffer.Span.SequenceEqual(other.Buffer.Span)
                && Address.Span.SequenceEqual(other.Address.Span)
                && Port == other.Port;
        }

        public override bool Equals(object obj)
        {
            return obj is Datagram datagram && Equals(datagram);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Buffer, Address, Port);
        }

        public static bool operator ==(Datagram left, Datagram right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Datagram left, Datagram right)
        {
            return !left.Equals(right);
        }

        internal IPAddress GetIPAddress()
        {
            return cachedIpAddress == null ? new IPAddress(Address.Span) : cachedIpAddress;
        }
    }
}
