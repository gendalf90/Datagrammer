using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Datagrammer
{
    internal class MessageParallelSafeHandlerComposer : IMessageHandler
    {
        private readonly List<IMessageHandler> handlers = new List<IMessageHandler>();

        private readonly IErrorHandler errorHandler;

        public MessageParallelSafeHandlerComposer(IErrorHandler errorHandler)
        {
            this.errorHandler = errorHandler;
        }

        public void AddHandler(IMessageHandler handler)
        {
            handlers.Add(handler);
        }

        public async Task HandleAsync(byte[] data, IPEndPoint endPoint)
        {
            var messageSafeHandlerTasks = handlers.Select(handler => HandleSafeAsync(handler, data, endPoint));
            await Task.WhenAll(messageSafeHandlerTasks);
        }

        private async Task HandleSafeAsync(IMessageHandler handler, byte[] data, IPEndPoint endPoint)
        {
            try
            {
                await handler.HandleAsync(data, endPoint);
            }
            catch(Exception e)
            {
                await errorHandler.HandleAsync(e);
            }
        }
    }
}
