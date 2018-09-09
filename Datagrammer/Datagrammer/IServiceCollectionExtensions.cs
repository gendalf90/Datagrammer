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

        private static IDatagramClient CreateDatagramClient(IServiceProvider provider)
        {
            var bootstrap = new Bootstrap();
            FillMessageHandlers(provider, bootstrap);
            FillErrorHandlers(provider, bootstrap);
            FillMiddlewares(provider, bootstrap);
            ConfigureIfNeeded(provider, bootstrap);
            SetCustomProtocolIfNeeded(provider, bootstrap);
            return bootstrap.Build();
        }

        private static void FillMessageHandlers(IServiceProvider provider, Bootstrap bootstrap)
        {
            var messageHandlers = provider.GetServices<IMessageHandler>();

            foreach (var handler in messageHandlers)
            {
                bootstrap.AddMessageHandler(handler);
            }
        }

        private static void FillErrorHandlers(IServiceProvider provider, Bootstrap bootstrap)
        {
            var errorHandlers = provider.GetServices<IErrorHandler>();

            foreach (var handler in errorHandlers)
            {
                bootstrap.AddErrorHandler(handler);
            }
        }

        private static void FillMiddlewares(IServiceProvider provider, Bootstrap bootstrap)
        {
            var middlewares = provider.GetServices<IMiddleware>();

            foreach (var middleware in middlewares)
            {
                bootstrap.AddMiddleware(middleware);
            }
        }

        private static void ConfigureIfNeeded(IServiceProvider provider, Bootstrap bootstrap)
        {
            var options = provider.GetService<IOptions<DatagramOptions>>();

            if(options == null)
            {
                return;
            }

            bootstrap.Configure(datagramOptions =>
            {
                datagramOptions.ListeningPoint = options.Value.ListeningPoint;
                datagramOptions.ReceivingParallelismDegree = options.Value.ReceivingParallelismDegree;
            });
        }

        private static void SetCustomProtocolIfNeeded(IServiceProvider provider, Bootstrap bootstrap)
        {
            var protocolCreator = provider.GetService<IProtocolCreator>();

            if(protocolCreator == null)
            {
                return;
            }

            bootstrap.UseCustomProtocol(protocolCreator);
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
