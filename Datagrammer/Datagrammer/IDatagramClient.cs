using Microsoft.Extensions.Hosting;

namespace Datagrammer
{
    public interface IDatagramClient : IDatagramSender, IHostedService
    {
    }
}
