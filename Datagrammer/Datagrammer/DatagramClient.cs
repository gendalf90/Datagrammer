using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private Task[] processingTasks;
        private BlockingCollection<Datagram> receivedMessages;
        private CancellationTokenSource messageProcessingCancellation;
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
            var handlerTasks = errorHandlers.Select(handler => handler.HandleAsync(e));
            await Task.WhenAll(handlerTasks);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            lock (synchronization)
            {
                ThrowErrorIfHasStarted();
                InitializeProtocol();
                StartProcessing();
                MarkAsStarted();
            }

            return Task.CompletedTask;
        }

        private void ThrowErrorIfHasStarted()
        {
            if (hasStarted)
            {
                throw new InvalidOperationException();
            }
        }

        private void MarkAsStarted()
        {
            hasStarted = true;
        }

        private void InitializeProtocol()
        {
            protocol = protocolCreator.Create(options.Value.ListeningPoint);
        }

        private void StartProcessing()
        {
            messageProcessingCancellation = new CancellationTokenSource();
            receivedMessages = new BlockingCollection<Datagram>(options.Value.ReceivingParallelismDegree);
            processingTasks = GetStartedProcessingTasks(options.Value.ReceivingParallelismDegree).ToArray();
        }

        private IEnumerable<Task> GetStartedProcessingTasks(int parallelismDegree)
        {
            yield return ReceiveMessageSafeAsync();

            for(int i = 0; i < parallelismDegree; i++)
            {
                yield return ProcessMessageSafeAsync();
            }
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
            finally
            {
                StopMessageProcessing();
            }
        }

        private async Task ReceiveMessageAsync()
        {
            while (true)
            {
                try
                {
                    var message = await protocol.ReceiveAsync();
                    receivedMessages.Add(message, messageProcessingCancellation.Token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    await HandleErrorAsync(e);
                    continue;
                }
            };
        }

        private async Task ProcessMessageSafeAsync()
        {
            try
            {
                await ProcessMessageAsync();
            }
            catch
            {
                CloseConnection();
                StopMessageProcessing();
                throw;
            }
        }

        private async Task ProcessMessageAsync()
        {
            while (true)
            {
                await Task.Yield();

                Datagram message;

                try
                {
                    message = receivedMessages.Take(messageProcessingCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
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
            }
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
                await handler.HandleAsync(message);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(e);
            }
        }

        private void CloseConnection()
        {
            protocol.Dispose();
        }

        private void StopMessageProcessing()
        {
            messageProcessingCancellation.Cancel();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            lock (synchronization)
            {
                ThrowErrorIfHasNotStarted();
                CloseConnection();
                WaitProcessingTasks();
            }
            
            return Task.CompletedTask;
        }

        private void ThrowErrorIfHasNotStarted()
        {
            if (!hasStarted)
            {
                throw new InvalidOperationException();
            }
        }

        private void WaitProcessingTasks()
        {
            Task.WaitAll(processingTasks);
        }
    }
}
