using System.Net;
using System.Net.Sockets;

namespace Datagrammer.SocketEventArgs
{
    internal class ReceivingSocketAsyncEventArgs : AwaitableSocketAsyncEventArgs
    {
        private AddressFamily addressFamily;

        public ReceivingSocketAsyncEventArgs(AddressFamily addressFamily)
        {
            this.addressFamily = addressFamily;

            RemoteEndPoint = new IPEndPoint(IPAddress.None, IPEndPoint.MinPort);

            SetAnyEndPoint();
            SetBuffer(new byte[MaxUDPSize], 0, MaxUDPSize);
        }

        private void SetAnyEndPoint()
        {
            var ipEndPoint = (IPEndPoint)RemoteEndPoint;

            if (addressFamily == AddressFamily.InterNetworkV6)
            {
                ipEndPoint.Address = IPAddress.IPv6Any;
            }
            else
            {
                ipEndPoint.Address = IPAddress.Any;
            }

            ipEndPoint.Port = IPEndPoint.MinPort;
        }

        public Datagram GetDatagram()
        {
            var ipEndPoint = (IPEndPoint)RemoteEndPoint;
            var address = ipEndPoint.Address.GetAddressBytes();
            var buffer = MemoryBuffer
                .Slice(0, BytesTransferred)
                .ToArray();

            return new Datagram(buffer, address, ipEndPoint.Port);
        }

        public override void Reset()
        {
            base.Reset();

            SetAnyEndPoint();
        }
    }
}
