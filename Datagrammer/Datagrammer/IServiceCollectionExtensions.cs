using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddHostedDatagrammer(this IServiceCollection services)
        {
            return services.AddSingleton(GetDatagramHostedServiceProxy)
                           .AddDatagrammer();
                           
        }

        public static IServiceCollection AddDatagrammer(this IServiceCollection services)
        {
            return services.AddSingleton(GetDatagramClientProxy)
                           .AddSingleton(GetDatagramSenderProxy)
                           .AddSingleton(CreateDatagramClient);
        }

        public static IDatagramClient BuildDatagramClient(this IServiceCollection services)
        {
            return services.AddDatagrammer()
                           .BuildServiceProvider()
                           .GetService<IDatagramClient>();
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

        private static IDatagramClient GetDatagramClientProxy(IServiceProvider provider)
        {
            return new DatagramClientProxy(provider);
        }

        private static IDatagramSender GetDatagramSenderProxy(IServiceProvider provider)
        {
            return new DatagramClientProxy(provider);
        }

        private static IHostedService GetDatagramHostedServiceProxy(IServiceProvider provider)
        {
            return new DatagramClientProxy(provider);
        }

        private class DatagramClientProxy : IDatagramClient
        {
            private readonly Lazy<IDatagramClient> client;

            public DatagramClientProxy(IServiceProvider provider)
            {
                client = new Lazy<IDatagramClient>(provider.GetService<IDatagramClient>, LazyThreadSafetyMode.PublicationOnly);
            }

            public Task SendAsync(Datagram message)
            {
                return client.Value.SendAsync(message);
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                return client.Value.StartAsync(cancellationToken);
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return client.Value.StopAsync(cancellationToken);
            }
        }
    }
}
