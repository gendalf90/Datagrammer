using System.Net.Sockets;

namespace Datagrammer
{
    internal static class SocketAsyncEventArgsExtensions
    {
        public static void SetBuffer(this SocketAsyncEventArgs args)
        {
            args.SetBuffer(new byte[Datagram.MaxSize], 0, Datagram.MaxSize);
        }

        public static void ThrowIfNotSuccess(this SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success)
            {
                throw new SocketException((int)args.SocketError);
            }
        }
    }
}
