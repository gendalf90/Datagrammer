using System.Threading.Tasks;

namespace Datagrammer
{
    public interface IMiddleware
    {
        Task<byte[]> ReceiveAsync(byte[] data);

        Task<byte[]> SendAsync(byte[] data);
    }
}
