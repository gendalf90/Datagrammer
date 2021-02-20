using System;

namespace Datagrammer
{
    public readonly struct Datagram : IEquatable<Datagram>
    {
        public const int MaxSize = 0x10000;

        public Datagram(ReadOnlyMemory<byte> buffer, ReadOnlyMemory<byte> address, int port)
        {
            Buffer = buffer;
            Address = address;
            Port = port;
        }

        public Datagram(ReadOnlyMemory<byte> buffer) : this(buffer, ReadOnlyMemory<byte>.Empty, 0)
        {
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
            return new HashCodeBuilder()
                .Combine(Buffer.Span)
                .Combine(Address.Span)
                .Combine(Port)
                .Build();
        }

        public static bool operator ==(Datagram left, Datagram right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Datagram left, Datagram right)
        {
            return !left.Equals(right);
        }

        public static Datagram Empty { get; } = new Datagram();
    }
}
