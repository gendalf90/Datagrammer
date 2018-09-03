using System.Net;
using System.Threading.Tasks;

namespace Datagrammer
{
    public interface ISender
    {
        Task SendAsync(byte[] data, IPEndPoint endPoint);
    }
}
