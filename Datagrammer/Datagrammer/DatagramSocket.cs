using System;
using System.Net.Sockets;

namespace Datagrammer
{
    public sealed class DatagramSocket : IDatagramSocket
    {
        private readonly Socket socket;

        public DatagramSocket(Socket socket)
        {
            this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
        }

        public static DatagramSocket Create(Action<Socket> configuration = null)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            configuration?.Invoke(socket);

            return new DatagramSocket(socket);
        }

        public bool ReceiveFromAsync(SocketAsyncEventArgs args)
        {
            return socket.ReceiveFromAsync(args);
        }

        public bool SendToAsync(SocketAsyncEventArgs args)
        {
            return socket.SendToAsync(args);
        }

        public void Dispose()
        {
            socket.Dispose();
        }
    }
}
