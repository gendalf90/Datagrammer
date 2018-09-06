using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer
{
    internal class Client : IClient
    {
        private readonly object synchronization = new object();

        private readonly IEnumerable<IErrorHandler> errorHandlers;
        private readonly IEnumerable<IMessageHandler> messageHandlers;
        private readonly IEnumerable<IMiddleware> middlewares;
        private readonly IMessageClientCreator messageClientCreator;
        private readonly IOptions<Options> options;

        private IMessageClient messageClient;
        private Task[] processingTasks;
        private BlockingCollection<MessageDto> receivedMessages;
        private CancellationTokenSource messageProcessingCancellation;
        private volatile bool hasStarted;

        public Client(IEnumerable<IErrorHandler> errorHandlers,
                      IEnumerable<IMessageHandler> messageHandlers,
                      IEnumerable<IMiddleware> middlewares,
                      IMessageClientCreator messageClientCreator,
                      IOptions<Options> options)
        {
            this.errorHandlers = errorHandlers;
            this.messageHandlers = messageHandlers;
            this.middlewares = middlewares;
            this.messageClientCreator = messageClientCreator;
            this.options = options;
        }

        public async Task SendAsync(byte[] data, IPEndPoint endPoint)
        {
            ThrowErrorIfHasNotStarted();

            try
            {
                await SendUnsafeAsync(data, endPoint);
            }
            catch
            {
                CloseConnection();
                throw;
            }
        }

        private async Task SendUnsafeAsync(byte[] data, IPEndPoint endPoint)
        {
            byte[] dataToSend = data;

            try
            {
                dataToSend = await ProcessBySendingPipelineAsync(dataToSend);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(e);
                return;
            }

            try
            {
                await messageClient.SendAsync(new MessageDto
                {
                    EndPoint = endPoint,
                    Bytes = dataToSend
                });
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

        private async Task<byte[]> ProcessBySendingPipelineAsync(byte[] data)
        {
            var processingData = data;

            foreach (var middleware in middlewares)
            {
                processingData = await middleware.SendAsync(processingData);
            }

            return processingData;
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
                InitializeMessageClient();
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

        private void InitializeMessageClient()
        {
            messageClient = messageClientCreator.Create(options.Value.ListeningPoint);
        }

        private void StartProcessing()
        {
            messageProcessingCancellation = new CancellationTokenSource();
            receivedMessages = new BlockingCollection<MessageDto>(options.Value.ReceivingParallelismDegree);
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
                    var message = await messageClient.ReceiveAsync();
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

                MessageDto message;

                try
                {
                    message = receivedMessages.Take(messageProcessingCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var processingData = message.Bytes;

                try
                {
                    processingData = await ProcessByReceivingPipelineAsync(processingData);
                }
                catch (Exception e)
                {
                    await HandleErrorAsync(e);
                    continue;
                }

                await HandleMessageAsync(processingData, message.EndPoint);
            }
        }

        private async Task<byte[]> ProcessByReceivingPipelineAsync(byte[] data)
        {
            var processingData = data;

            foreach (var middleware in middlewares.Reverse())
            {
                processingData = await middleware.ReceiveAsync(processingData);
            }

            return processingData;
        }

        public async Task HandleMessageAsync(byte[] data, IPEndPoint endPoint)
        {
            var messageSafeHandlerTasks = messageHandlers.Select(handler => HandleMessageSafeAsync(handler, data, endPoint));
            await Task.WhenAll(messageSafeHandlerTasks);
        }

        private async Task HandleMessageSafeAsync(IMessageHandler handler, byte[] data, IPEndPoint endPoint)
        {
            try
            {
                await handler.HandleAsync(data, endPoint);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(e);
            }
        }

        private void CloseConnection()
        {
            messageClient.Dispose();
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
