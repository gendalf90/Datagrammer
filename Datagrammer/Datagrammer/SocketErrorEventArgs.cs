using System;
using System.Net.Sockets;

namespace Datagrammer
{
    public sealed class SocketErrorEventArgs : EventArgs
    {
        public SocketErrorEventArgs(SocketException error)
        {
            Error = error;
        }

        public SocketException Error { get; }
    }
}
