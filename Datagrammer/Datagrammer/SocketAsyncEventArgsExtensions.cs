using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Datagrammer
{
    internal static class SocketAsyncEventArgsExtensions
    {
        private const int MaxUDPSize = 0x10000;

        public static void SetDatagram(this SocketAsyncEventArgs args, Datagram datagram)
        {
            try
            {
                args.RemoteEndPoint = datagram.GetEndPoint();
            }
            catch
            {
                throw new SocketException((int)SocketError.AddressNotAvailable);
            }

            if (datagram.Buffer.Length > MaxUDPSize)
            {
                throw new SocketException((int)SocketError.MessageSize);
            }

            args.SetBuffer(MemoryMarshal.AsMemory(datagram.Buffer));
        }

        public static Datagram GetDatagram(this SocketAsyncEventArgs args)
        {
            var ipEndPoint = (IPEndPoint)args.RemoteEndPoint;
            var address = ipEndPoint.Address.GetAddressBytes();
            var buffer = args.MemoryBuffer
                .Slice(0, args.BytesTransferred)
                .ToArray();

            return new Datagram(buffer, address, ipEndPoint.Port);
        }

        public static void ThrowIfNotSuccess(this SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success)
            {
                throw new SocketException((int)args.SocketError);
            }
        }

        public static void InitializeForReceiving(this SocketAsyncEventArgs args)
        {
            args.RemoteEndPoint = new IPEndPoint(IPAddress.None, IPEndPoint.MinPort);
            args.SetBuffer(new byte[MaxUDPSize], 0, MaxUDPSize);
        }
    }
}
