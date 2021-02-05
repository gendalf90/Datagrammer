using System;
using System.Net.Sockets;

namespace Datagrammer
{
    internal static class SocketAsyncEventArgsExtensions
    {
        public static void ThrowIfNotSuccess(this SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success)
            {
                throw new SocketException((int)args.SocketError);
            }
        }

        public static Memory<byte> InitializeBuffer(this SocketAsyncEventArgs args)
        {
            Memory<byte> buffer = new byte[DatagramBuffer.MaxSize];

            args.SetBuffer(buffer);

            return buffer;
        }
    }
}
