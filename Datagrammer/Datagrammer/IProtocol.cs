using System;
using System.Threading.Tasks;

namespace Datagrammer
{
    public interface IProtocol : IDisposable
    {
        Task<Datagram> ReceiveAsync();

        Task SendAsync(Datagram message);
    }
}
