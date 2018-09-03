using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datagrammer
{
    internal class MiddlewareComposer : IMiddleware
    {
        private Queue<IMiddleware> sendingPipeline = new Queue<IMiddleware>();
        private Stack<IMiddleware> receivingPipeline = new Stack<IMiddleware>();

        public void AddMiddleware(IMiddleware middleware)
        {
            sendingPipeline.Enqueue(middleware);
            receivingPipeline.Push(middleware);
        }

        public async Task<byte[]> ReceiveAsync(byte[] data)
        {
            var processingData = data;

            foreach(var middleware in receivingPipeline)
            {
                processingData = await middleware.ReceiveAsync(processingData);
            }

            return processingData;
        }

        public async Task<byte[]> SendAsync(byte[] data)
        {
            var processingData = data;

            foreach (var middleware in sendingPipeline)
            {
                processingData = await middleware.SendAsync(processingData);
            }

            return processingData;
        }
    }
}
