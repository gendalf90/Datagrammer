using System.Threading.Tasks;

namespace Datagrammer
{
    public interface IMiddleware
    {
        Task<byte[]> ReceiveAsync(byte[] bytes);

        Task<byte[]> SendAsync(byte[] bytes);
    }
}
