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
            return services.AddSingleton(BuildHostedDatagramClientProxy)
                           .AddSingleton<IDatagramClient>(provider =>
                           {
                               return provider.GetService<HostedDatagramClientProxy>();
                           })
                           .AddSingleton<IDatagramSender>(provider =>
                           {
                               return provider.GetService<HostedDatagramClientProxy>();
                           })
                           .AddSingleton<IHostedService>(provider =>
                           {
                               return provider.GetService<HostedDatagramClientProxy>();
                           });
        }

        public static IServiceCollection AddDatagrammer(this IServiceCollection services)
        {
            return services.AddSingleton(BuildStartedDatagramClient)
                           .AddSingleton<IDatagramClient>(provider =>
                           {
                               return provider.GetService<DatagramClient>();
                           })
                           .AddSingleton<IDatagramSender>(provider =>
                           {
                               return provider.GetService<DatagramClient>();
                           });
        }

        private static DatagramClient BuildDatagramClient(IServiceProvider provider)
        {
            var messageHandlers = provider.GetServices<IMessageHandler>();
            var errorHandlers = provider.GetServices<IErrorHandler>();
            var middlewares = provider.GetServices<IMiddleware>();
            var options = provider.GetService<IOptions<DatagramOptions>>() ?? new OptionsWrapper<DatagramOptions>(new DatagramOptions());
            var protocolCreator = provider.GetService<IProtocolCreator>() ?? new ProtocolCreator();

            return new DatagramClient(errorHandlers,
                                      messageHandlers,
                                      middlewares,
                                      protocolCreator,
                                      options);
        }

        private static DatagramClient BuildStartedDatagramClient(IServiceProvider provider)
        {
            var client = BuildDatagramClient(provider);
            client.Start();
            return client;
        }

        private static HostedDatagramClientProxy BuildHostedDatagramClientProxy(IServiceProvider provider)
        {
            var client = BuildDatagramClient(provider);
            return new HostedDatagramClientProxy(client);
        }

        private class HostedDatagramClientProxy : IDatagramClient, IHostedService
        {
            private readonly DatagramClient client;

            public HostedDatagramClientProxy(DatagramClient client)
            {
                this.client = client;
            }

            public Task SendAsync(Datagram message)
            {
                return client.SendAsync(message);
            }

            public void Dispose()
            {
                client.Dispose();
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                client.Start();
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                client.Stop();
                return Task.CompletedTask;
            }

            public void Stop()
            {
                client.Stop();
            }
        }
    }
}
