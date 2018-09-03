using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datagrammer
{
    internal class ErrorParallelHandlerComposer : IErrorHandler
    {
        private readonly List<IErrorHandler> handlers = new List<IErrorHandler>();

        public void AddHandler(IErrorHandler handler)
        {
            handlers.Add(handler);
        }

        public async Task HandleAsync(Exception e)
        {
            var handlerTasks = handlers.Select(handler => handler.HandleAsync(e));
            await Task.WhenAll(handlerTasks);
        }
    }
}
