using System.Threading.Tasks;

namespace Datagrammer
{
    public interface IStoppingHandler
    {
        Task HandleAsync();
    }
}
