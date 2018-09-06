using Microsoft.Extensions.Options;
using System;
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
        private bool isStarted;

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

            if (messageClient == null)
            {
                throw new InvalidOperationException();
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
                StartUdpClient();
                StartProcessing();
                MarkAsStarted();
            }

            return Task.CompletedTask;
        }

        private void ThrowErrorIfHasStarted()
        {
            if(isStarted)
            {
                throw new InvalidOperationException();
            }
        }

        private void MarkAsStarted()
        {
            isStarted = true;
        }

        private void StartUdpClient()
        {
            messageClient = messageClientCreator.Create(options.Value.ListeningPoint);
        }

        private void StartProcessing()
        {
            processingTasks = new Task[options.Value.ReceivingParallelismDegree];
            
            for (int i = 0; i < processingTasks.Length; i++)
            {
                processingTasks[i] = ProcessSafeAsync();
            }
        }

        private async Task ProcessSafeAsync()
        {
            try
            {
                await ProcessAsync();
            }
            catch
            {
                CloseConnection();
                throw;
            }
        }

        private async Task ProcessAsync()
        {
            while (true)
            {
                MessageDto receiveResult;

                try
                {
                    receiveResult = await messageClient.ReceiveAsync();
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

                var processingData = receiveResult.Bytes;

                try
                {
                    processingData = await ProcessByReceivingPipelineAsync(processingData);
                }
                catch (Exception e)
                {
                    await HandleErrorAsync(e);
                    continue;
                }

                await HandleMessageAsync(processingData, receiveResult.EndPoint);
            };
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
            var messageSafeHandlerTasks = messageHandlers.Select(handler => HandleSafeAsync(handler, data, endPoint));
            await Task.WhenAll(messageSafeHandlerTasks);
        }

        private async Task HandleSafeAsync(IMessageHandler handler, byte[] data, IPEndPoint endPoint)
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
            messageClient?.Dispose();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            lock (synchronization)
            {
                MarkAsNotStarted();
                CloseConnection();
                WaitProcessingTasks();
            }

            return Task.CompletedTask;
        }

        private void WaitProcessingTasks()
        {
            Task.WaitAll(processingTasks);
        }

        private void MarkAsNotStarted()
        {
            isStarted = false;
        }
    }
}
