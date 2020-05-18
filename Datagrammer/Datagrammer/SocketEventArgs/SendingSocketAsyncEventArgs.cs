using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Datagrammer.SocketEventArgs
{
    internal class SendingSocketAsyncEventArgs : AwaitableSocketAsyncEventArgs
    {
        public SendingSocketAsyncEventArgs()
        {
            RemoteEndPoint = new IPEndPoint(IPAddress.None, IPEndPoint.MinPort);
        }

        public void SetDatagram(Datagram datagram)
        {
            var ipEndPoint = (IPEndPoint)RemoteEndPoint;

            try
            {
                ipEndPoint.Address = new IPAddress(datagram.Address.Span);
                ipEndPoint.Port = datagram.Port;
            }
            catch
            {
                throw new SocketException((int)SocketError.AddressNotAvailable);
            }

            if(datagram.Buffer.Length > MaxUDPSize)
            {
                throw new SocketException((int)SocketError.MessageSize);
            }

            if(MemoryMarshal.TryGetArray(datagram.Buffer, out var bufferSegment))
            {
                SetBuffer(bufferSegment);
            }
            else
            {
                SetBuffer(datagram.Buffer.ToArray());
            }
        }
    }
}
