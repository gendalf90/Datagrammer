using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Datagrammer
{
    internal class MessageParallelHandlerComposer : IMessageHandler
    {
        private readonly List<IMessageHandler> handlers = new List<IMessageHandler>();

        public void AddHandler(IMessageHandler handler)
        {
            handlers.Add(handler);
        }

        public async Task HandleAsync(byte[] data, IPEndPoint endPoint)
        {
            var handlerTasks = handlers.Select(handler => handler.HandleAsync(data, endPoint));
            await Task.WhenAll(handlerTasks);
        }
    }
}
