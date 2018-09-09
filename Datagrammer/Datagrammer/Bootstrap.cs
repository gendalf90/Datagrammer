using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace Datagrammer
{
    public sealed class Bootstrap
    {
        private IList<IMessageHandler> messageHandlers;
        private IList<IErrorHandler> errorHandlers;
        private IList<IMiddleware> middlewares;
        private IOptions<DatagramOptions> options;
        private IProtocolCreator protocolCreator;

        public Bootstrap()
        {
            messageHandlers = new List<IMessageHandler>();
            errorHandlers = new List<IErrorHandler>();
            middlewares = new List<IMiddleware>();
            options = new OptionsWrapper<DatagramOptions>(new DatagramOptions());
            protocolCreator = new ProtocolCreator();
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
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            errorHandlers.Add(handler);
            return this;
        }

        public Bootstrap AddMiddleware(IMiddleware middleware)
        {
            if (middleware == null)
            {
                throw new ArgumentNullException(nameof(middleware));
            }

            middlewares.Add(middleware);
            return this;
        }

        public Bootstrap Configure(Action<DatagramOptions> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            configure(options.Value);

            if(options.Value.ListeningPoint == null)
            {
                throw new ArgumentNullException(nameof(options.Value.ListeningPoint));
            }

            if(options.Value.ReceivingParallelismDegree <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.Value.ReceivingParallelismDegree));
            }

            return this;
        }

        public Bootstrap UseCustomProtocol(IProtocolCreator protocolCreator)
        {
            this.protocolCreator = protocolCreator ?? throw new ArgumentNullException(nameof(protocolCreator));
            return this;
        }

        public IDatagramClient Build()
        {
            return new DatagramClient(errorHandlers,
                                      messageHandlers,
                                      middlewares,
                                      protocolCreator,
                                      options);
        }
    }
}
