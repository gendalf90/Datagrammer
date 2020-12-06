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
            if(port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            return new Datagram(datagram.Buffer, datagram.Address, port);
        }

        public static IPEndPoint GetEndPoint(this Datagram datagram)
        {
            return new IPEndPoint(new IPAddress(datagram.Address.Span), datagram.Port);
        }

        public static IPAddress GetAddress(this Datagram datagram)
        {
            return new IPAddress(datagram.Address.Span);
        }
    }
}
