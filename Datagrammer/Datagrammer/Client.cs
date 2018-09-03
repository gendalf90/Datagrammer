using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer
{
    internal class Client : IClient
    {
        private readonly IErrorHandler errorHandler;
        private readonly IMessageHandler messageHandler;
        private readonly IMiddleware middleware;

        private readonly IPEndPoint udpListeningPoint;

        private UdpClient udpClient;
        private Task processingTask;

        public Client(IErrorHandler errorHandler,
                      IMessageHandler messageHandler,
                      IMiddleware middleware,
                      IPEndPoint udpListeningPoint)
        {
            this.errorHandler = errorHandler;
            this.messageHandler = messageHandler;
            this.middleware = middleware;
            this.udpListeningPoint = udpListeningPoint;
        }

        public async Task SendAsync(byte[] data, IPEndPoint endPoint)
        {
            try
            {
                await SendUnsafeAsync(data, endPoint);
            }
            catch
            {
                udpClient?.Close();
                throw;
            }
        }

        private async Task SendUnsafeAsync(byte[] data, IPEndPoint endPoint)
        {
            byte[] dataToSend = data;

            try
            {
                dataToSend = await middleware.SendAsync(dataToSend);
            }
            catch (Exception e)
            {
                await errorHandler.HandleAsync(e);
                return;
            }

            if (udpClient == null)
            {
                throw new InvalidOperationException();
            }

            try
            {
                await udpClient.SendAsync(dataToSend, dataToSend.Length, endPoint);
            }
            catch (ObjectDisposedException)
            {
                throw new InvalidOperationException();
            }
            catch (Exception e)
            {
                await errorHandler.HandleAsync(e);
                return;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartUdpClient();
            StartProcessing();
            return Task.CompletedTask;
        }

        private void StartUdpClient()
        {
            udpClient = new UdpClient(udpListeningPoint);
        }

        private void StartProcessing()
        {
            processingTask = ProcessSafeAsync();
        }

        private async Task ProcessSafeAsync()
        {
            try
            {
                await ProcessAsync();
            }
            catch
            {
                udpClient.Close();
                throw;
            }
        }

        private async Task ProcessAsync()
        {
            while (true)
            {
                UdpReceiveResult udpReceiveResult;

                try
                {
                    udpReceiveResult = await udpClient.ReceiveAsync();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    await errorHandler.HandleAsync(e);
                    continue;
                }

                try
                {
                    var processedData = await middleware.ReceiveAsync(udpReceiveResult.Buffer);
                    await messageHandler.HandleAsync(processedData, udpReceiveResult.RemoteEndPoint);
                }
                catch (Exception e)
                {
                    await errorHandler.HandleAsync(e);
                    continue;
                }
            };
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            udpClient?.Close();
            
            if(processingTask != null)
            {
                await processingTask;
            }
        }
    }
}
