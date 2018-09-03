using System;
using System.Net;

namespace Datagrammer
{
    public sealed class Bootstrap
    {
        private readonly ErrorParallelHandlerComposer errorHandlerComposer;
        private readonly MessageParallelSafeHandlerComposer messageHandlerComposer;
        private readonly MiddlewareComposer middlewareComposer;

        public Bootstrap()
        {
            errorHandlerComposer = new ErrorParallelHandlerComposer();
            messageHandlerComposer = new MessageParallelSafeHandlerComposer(errorHandlerComposer);
            middlewareComposer = new MiddlewareComposer();
        }

        public Bootstrap AddMessageHandler(IMessageHandler handler)
        {
            if(handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            messageHandlerComposer.AddHandler(handler);
            return this;
        }

        public Bootstrap AddErrorHandler(IErrorHandler handler)
        {
            if(handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            errorHandlerComposer.AddHandler(handler);
            return this;
        }

        public Bootstrap AddMiddleware(IMiddleware middleware)
        {
            if(middleware == null)
            {
                throw new ArgumentNullException(nameof(middleware));
            }

            middlewareComposer.AddMiddleware(middleware);
            return this;
        }

        public IClient Create(IPEndPoint udpListeningPoint)
        {
            if(udpListeningPoint == null)
            {
                throw new ArgumentNullException(nameof(udpListeningPoint));
            }

            return new Client(errorHandlerComposer,
                              messageHandlerComposer,
                              middlewareComposer,
                              udpListeningPoint);
        }
    }
}
