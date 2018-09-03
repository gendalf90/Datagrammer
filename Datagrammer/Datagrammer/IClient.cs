using Microsoft.Extensions.Hosting;

namespace Datagrammer
{
    public interface IClient : ISender, IHostedService
    {
    }
}
