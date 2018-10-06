using System.Threading.Tasks;

namespace Datagrammer
{
    public interface IMiddleware
    {
        Task<Datagram> ReceiveAsync(Datagram message);

        Task<Datagram> SendAsync(Datagram message);
    }
}
