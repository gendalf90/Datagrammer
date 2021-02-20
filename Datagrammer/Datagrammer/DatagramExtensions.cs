using System;
using System.Net;

namespace Datagrammer
{
    public static class DatagramExtensions
    {
        public static Datagram WithBuffer(this Datagram datagram, ReadOnlyMemory<byte> buffer)
        {
            return new Datagram(buffer, datagram.Address, datagram.Port);
        }

        public static Datagram WithEndPoint(this Datagram datagram, IPEndPoint endPoint)
        {
            return new Datagram(datagram.Buffer, endPoint.Address.GetAddressBytes(), endPoint.Port);
        }

        public static Datagram WithAddress(this Datagram datagram, IPAddress ipAddress)
        {
            return new Datagram(datagram.Buffer, ipAddress.GetAddressBytes(), datagram.Port);
        }

        public static Datagram WithAddress(this Datagram datagram, params byte[] ipBytes)
        {
            return datagram.WithAddress(new IPAddress(ipBytes));
        }

        public static Datagram WithAddress(this Datagram datagram, string ipString)
        {
            return datagram.WithAddress(IPAddress.Parse(ipString));
        }

        public static Datagram WithPort(this Datagram datagram, int port)
        {
            if(!IsValidPort(port))
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            return new Datagram(datagram.Buffer, datagram.Address, port);
        }

        public static IPEndPoint GetEndPoint(this Datagram datagram)
        {
            return new IPEndPoint(new IPAddress(datagram.Address.Span), datagram.Port);
        }

        public static bool TryGetEndPoint(this Datagram datagram, out IPEndPoint endPoint)
        {
            endPoint = null;

            if (!IsValidIPAddressBytes(datagram.Address) || !IsValidPort(datagram.Port))
            {
                return false;
            }

            endPoint = datagram.GetEndPoint();

            return true;
        }

        public static IPAddress GetAddress(this Datagram datagram)
        {
            return new IPAddress(datagram.Address.Span);
        }

        public static bool TryGetAddress(this Datagram datagram, out IPAddress address)
        {
            address = null;

            if (!IsValidIPAddressBytes(datagram.Address))
            {
                return false;
            }

            address = datagram.GetAddress();

            return true;
        }

        public static bool IsEndPointEmpty(this Datagram datagram)
        {
            return datagram.Address.IsEmpty && datagram.Port == 0;
        }

        public static Try<Datagram> AsTry(this Datagram datagram)
        {
            return new Try<Datagram>(datagram);
        }

        private static bool IsValidPort(int port)
        {
            return port >= IPEndPoint.MinPort && port <= IPEndPoint.MaxPort;
        }

        private static bool IsValidIPAddressBytes(ReadOnlyMemory<byte> bytes)
        {
            return bytes.Length == 4 || bytes.Length == 16;
        }
    }
}
