using System.Net;
using System.Threading.Tasks;

namespace Datagrammer
{
    public interface IDatagramSender
    {
        Task SendAsync(Datagram message);
    }
}
