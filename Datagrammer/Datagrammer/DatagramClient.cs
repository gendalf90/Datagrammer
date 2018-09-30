using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datagrammer
{
    internal class DatagramClient : IDatagramClient
    {
        private readonly object synchronization = new object();

        private readonly IEnumerable<IErrorHandler> errorHandlers;
        private readonly IEnumerable<IMessageHandler> messageHandlers;
        private readonly IEnumerable<IMiddleware> middlewares;
        private readonly IProtocolCreator protocolCreator;
        private readonly IOptions<DatagramOptions> options;

        private IProtocol protocol;
        private Task processingTask;
        private volatile bool hasStarted;

        public DatagramClient(IEnumerable<IErrorHandler> errorHandlers,
                              IEnumerable<IMessageHandler> messageHandlers,
                              IEnumerable<IMiddleware> middlewares,
                              IProtocolCreator protocolCreator,
                              IOptions<DatagramOptions> options)
        {
            this.errorHandlers = errorHandlers;
            this.messageHandlers = messageHandlers;
            this.middlewares = middlewares;
            this.protocolCreator = protocolCreator;
            this.options = options;
        }

        public async Task SendAsync(Datagram message)
        {
            ThrowErrorIfHasNotStarted();

            try
            {
                await SendUnsafeAsync(message);
            }
            catch
            {
                CloseConnection();
                throw;
            }
        }

        private async Task SendUnsafeAsync(Datagram message)
        {
            try
            {
                await ProcessBySendingPipelineAsync(message);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(e);
                return;
            }

            try
            {
                await protocol.SendAsync(message);
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (Exception e)
            {
                await HandleErrorAsync(e);
            }
        }

        private async Task ProcessBySendingPipelineAsync(Datagram message)
        {
            foreach (var middleware in middlewares)
            {
                message.Bytes = await middleware.SendAsync(message.Bytes);
            }
        }

        private async Task HandleErrorAsync(Exception e)
        {
            var context = CreateContext();
            var handlerTasks = errorHandlers.Select(handler => handler.HandleAsync(context, e));
            await Task.WhenAll(handlerTasks);
        }

        public void Start()
        {
            lock (synchronization)
            {
                ThrowErrorIfHasStarted();
                ValidateOptions();
                InitializeProtocol();
                StartProcessing();
                MarkAsStarted();
            }
        }

        private void ThrowErrorIfHasStarted()
        {
            if (hasStarted)
            {
                throw new InvalidOperationException();
            }
        }

        private void ValidateOptions()
        {
            if (options.Value.ListeningPoint == null)
            {
                throw new ArgumentNullException(nameof(options.Value.ListeningPoint));
            }
        }

        private void MarkAsStarted()
        {
            hasStarted = true;
        }

        private void InitializeProtocol()
        {
            protocol = protocolCreator.Create(options.Value.ListeningPoint) ?? throw new ArgumentNullException(nameof(protocol));
        }

        private void StartProcessing()
        {
            processingTask = ReceiveMessageSafeAsync();
        }

        private async Task ReceiveMessageSafeAsync()
        {
            try
            {
                await ReceiveMessageAsync();
            }
            catch
            {
                CloseConnection();
                throw;
            }
        }

        private async Task ReceiveMessageAsync()
        {
            while (true)
            {
                Datagram message;

                try
                {
                    message = await protocol.ReceiveAsync();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    await HandleErrorAsync(e);
                    continue;
                }

                try
                {
                    await ProcessByReceivingPipelineAsync(message);
                }
                catch (Exception e)
                {
                    await HandleErrorAsync(e);
                    continue;
                }

                await HandleMessageAsync(message);
            };
        }

        private async Task ProcessByReceivingPipelineAsync(Datagram message)
        {
            foreach (var middleware in middlewares.Reverse())
            {
                message.Bytes = await middleware.ReceiveAsync(message.Bytes);
            }
        }

        public async Task HandleMessageAsync(Datagram message)
        {
            var messageSafeHandlerTasks = messageHandlers.Select(handler => HandleMessageSafeAsync(handler, message));
            await Task.WhenAll(messageSafeHandlerTasks);
        }

        private async Task HandleMessageSafeAsync(IMessageHandler handler, Datagram message)
        {
            try
            {
                var context = CreateContext();
                await handler.HandleAsync(context, message);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(e);
            }
        }

        private IContext CreateContext()
        {
            return new Context(this);
        }

        private void CloseConnection()
        {
            protocol?.Dispose();
        }

        public void Dispose()
        {
            lock (synchronization)
            {
                CloseConnection();
            }
        }

        public void Stop()
        {
            lock (synchronization)
            {
                ThrowErrorIfHasNotStarted();
                CloseConnection();
                WaitProcessingTask();
            }
        }

        private void ThrowErrorIfHasNotStarted()
        {
            if (!hasStarted)
            {
                throw new InvalidOperationException();
            }
        }

        private void WaitProcessingTask()
        {
            processingTask.Wait();
        }
    }
}
