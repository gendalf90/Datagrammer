using System;
using System.Threading.Tasks;

namespace Datagrammer
{
    internal interface IMessageClient : IDisposable
    {
        Task<MessageDto> ReceiveAsync();

        Task SendAsync(MessageDto data);
    }
}
