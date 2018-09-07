using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;

namespace Datagrammer
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddHostedDatagrammer(this IServiceCollection services)
        {
            return services.AddSingleton(CreateDatagramClient)
                           .AddSingleton(GetDatagramSender)
                           .AddSingleton(GetDatagramHostedService);
        }

        public static IDatagramClient BuildDatagramClient(this IServiceCollection services)
        {
            var provider = services.BuildServiceProvider(true);
            return CreateDatagramClient(provider);
        }

        private static IDatagramClient CreateDatagramClient(IServiceProvider provider)
        {
            var messageHandlers = provider.GetServices<IMessageHandler>();
            var errorHandlers = provider.GetServices<IErrorHandler>();
            var middlewares = provider.GetServices<IMiddleware>();
            var options = provider.GetService<IOptions<DatagramOptions>>() ?? new OptionsWrapper<DatagramOptions>(new DatagramOptions());

            if(options.Value.ReceivingParallelismDegree <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.Value.ReceivingParallelismDegree));
            }

            if(options.Value.ListeningPoint == null)
            {
                throw new ArgumentNullException(nameof(options.Value.ListeningPoint));
            }

            var protocolCreator = provider.GetService<IProtocolCreator>() ?? new ProtocolCreator(options);

            return new DatagramClient(errorHandlers,
                                      messageHandlers,
                                      middlewares,
                                      protocolCreator,
                                      options);
        }

        private static IDatagramSender GetDatagramSender(IServiceProvider provider)
        {
            return provider.GetService<IDatagramClient>();
        }

        private static IHostedService GetDatagramHostedService(IServiceProvider provider)
        {
            return provider.GetService<IDatagramClient>();
        }
    }
}
