using System.Net;
using System.Threading.Tasks;

namespace Datagrammer
{
    public interface IMessageHandler
    {
        Task HandleAsync(byte[] data, IPEndPoint endPoint);
    }
}
