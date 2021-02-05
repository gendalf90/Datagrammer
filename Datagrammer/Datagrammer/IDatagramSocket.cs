using System;
using System.Net.Sockets;

namespace Datagrammer
{
    public interface IDatagramSocket : IDisposable
    {
        bool ReceiveFromAsync(SocketAsyncEventArgs args);

        bool SendToAsync(SocketAsyncEventArgs args);
    }
}
