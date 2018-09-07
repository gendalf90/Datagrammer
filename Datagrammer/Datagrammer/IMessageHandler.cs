using System.Threading.Tasks;

namespace Datagrammer
{
    public interface IMessageHandler
    {
        Task HandleAsync(Datagram message);
    }
}
