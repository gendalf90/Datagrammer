using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net;

namespace Datagrammer
{
    public sealed class Bootstrap
    {
        private readonly List<IErrorHandler> errorHandlers;
        private readonly List<IMessageHandler> messageHandlers;
        private readonly List<IMiddleware> middlewares;
        private readonly MessageClientCreator messageClientCreator;
        private readonly Options options;

        public Bootstrap()
        {
            errorHandlers = new List<IErrorHandler>();
            messageHandlers = new List<IMessageHandler>();
            middlewares = new List<IMiddleware>();
            messageClientCreator = new MessageClientCreator();
            options = new Options
            {
                ReceivingParallelismDegree = 1
            };
        }

        public Bootstrap AddMessageHandler(IMessageHandler handler)
        {
            if(handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            messageHandlers.Add(handler);
            return this;
        }

        public Bootstrap AddErrorHandler(IErrorHandler handler)
        {
            if(handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            errorHandlers.Add(handler);
            return this;
        }

        public Bootstrap AddMiddleware(IMiddleware middleware)
        {
            if(middleware == null)
            {
                throw new ArgumentNullException(nameof(middleware));
            }

            middlewares.Add(middleware);
            return this;
        }

        public Bootstrap SetReceivingParallelismDegree(int degree)
        {
            if(degree <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(degree));
            }

            options.ReceivingParallelismDegree = degree;
            return this;
        }

        public IClient Create(IPEndPoint listeningPoint)
        {
            options.ListeningPoint = listeningPoint ?? throw new ArgumentNullException(nameof(listeningPoint));

            return new Client(errorHandlers,
                              messageHandlers,
                              middlewares,
                              messageClientCreator,
                              new OptionsWrapper<Options>(options));
        }
    }
}
