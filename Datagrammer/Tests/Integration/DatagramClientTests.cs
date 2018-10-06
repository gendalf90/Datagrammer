using Datagrammer;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Tests.Integration
{
    public class DatagramClientTests
    {
        private readonly IPEndPoint firstEndPoint = new IPEndPoint(IPAddress.Loopback, 50001);
        private readonly IPEndPoint secondEndPoint = new IPEndPoint(IPAddress.Loopback, 50002);
        private readonly TimeSpan timeout = TimeSpan.FromSeconds(3);

        [Fact]
        public async Task SendingAndHandling()
        {
            var toSend = new List<Datagram>
            {
                new Datagram { Bytes = new byte[] { 1, 2, 3 }, EndPoint = firstEndPoint },
                new Datagram { Bytes = new byte[] { 4, 5, 6 }, EndPoint = firstEndPoint },
                new Datagram { Bytes = new byte[] { 7, 8, 9 }, EndPoint = firstEndPoint },
                new Datagram { Bytes = new byte[] { 10, 11, 12 }, EndPoint = firstEndPoint },
                new Datagram { Bytes = new byte[] { 13, 14, 15 }, EndPoint = firstEndPoint }
            };
            var received = new ConcurrentBag<Datagram>();
            var firstHandler = new Mock<IMessageHandler>();
            firstHandler.Setup(handler => handler.HandleAsync(It.IsAny<IContext>(), It.IsAny<Datagram>()))
                        .Callback<IContext, Datagram>((context, message) => received.Add(message))
                        .Returns(Task.CompletedTask);
            using (var firstClient = new Bootstrap().AddMessageHandler(firstHandler.Object)
                                                    .Configure(options =>
                                                    {
                                                        options.ListeningPoint = firstEndPoint;
                                                    })
                                                    .Build())
            using (var secondClient = new Bootstrap().Configure(options => options.ListeningPoint = secondEndPoint)
                                                     .Build())
            {

                foreach (var datagram in toSend)
                {
                    await secondClient.SendAsync(datagram);
                }
                await Task.Delay(timeout);
            }

            received.Select(message => message.Bytes).Should().BeEquivalentTo(toSend.Select(message => message.Bytes));
            received.Select(message => message.EndPoint).Should().OnlyContain(endPoint => endPoint.Equals(secondEndPoint));
        }

        [Fact]
        public async Task ErrorHandling()
        {
            var middlewareErrorMessage = "middleware error";
            var middlewareMock = new Mock<IMiddleware>();
            middlewareMock.Setup(middleware => middleware.SendAsync(It.IsAny<Datagram>()))
                          .ReturnsAsync<Datagram, IMiddleware, Datagram>(sent => sent);
            middlewareMock.Setup(middleware => middleware.ReceiveAsync(It.IsAny<Datagram>()))
                          .ThrowsAsync(new Exception(middlewareErrorMessage));
            var errorMiddlewareHandlerMock = new Mock<IErrorHandler>();
            errorMiddlewareHandlerMock.Setup(handler => handler.HandleAsync(It.IsAny<IContext>(), It.Is<Exception>(e => e.Message == middlewareErrorMessage)))
                                      .Returns(Task.CompletedTask)
                                      .Verifiable();
            var messageHandlerErrorMessage = "handler error";
            var messageHandlerMock = new Mock<IMessageHandler>();
            messageHandlerMock.Setup(handler => handler.HandleAsync(It.IsAny<IContext>(), It.IsAny<Datagram>()))
                              .ThrowsAsync(new Exception(messageHandlerErrorMessage));
            var errorHandlerMock = new Mock<IErrorHandler>();
            errorHandlerMock.Setup(handler => handler.HandleAsync(It.IsAny<IContext>(), It.Is<Exception>(e => e.Message == messageHandlerErrorMessage)))
                            .Returns(Task.CompletedTask)
                            .Verifiable();
            using (var clientWithMiddleware = new Bootstrap().AddErrorHandler(errorMiddlewareHandlerMock.Object)
                                                             .AddMiddleware(middlewareMock.Object)
                                                             .Configure(options =>
                                                             {
                                                                 options.ListeningPoint = firstEndPoint;
                                                             })
                                                             .Build())
            using (var clientWithMessageHandler = new Bootstrap().AddMessageHandler(messageHandlerMock.Object)
                                                                 .AddErrorHandler(errorHandlerMock.Object)
                                                                 .Configure(options =>
                                                                 {
                                                                     options.ListeningPoint = secondEndPoint;
                                                                 })
                                                                 .Build())
            {
                await clientWithMiddleware.SendAsync(new Datagram
                {
                    Bytes = new byte[] { 1, 2, 3 },
                    EndPoint = firstEndPoint
                });
                await clientWithMessageHandler.SendAsync(new Datagram
                {
                    Bytes = new byte[] { 1, 2, 3 },
                    EndPoint = secondEndPoint
                });
                await Task.Delay(timeout);
            }

            Mock.Verify(errorHandlerMock, errorMiddlewareHandlerMock);
        }

        [Fact]
        public async Task Stopping()
        {
            var message = new Datagram { Bytes = new byte[] { 1, 2, 3 }, EndPoint = firstEndPoint };
            var messageHandlerMock = new Mock<IMessageHandler>();
            messageHandlerMock.Setup(handler => handler.HandleAsync(It.IsAny<IContext>(), It.IsAny<Datagram>()))
                              .ThrowsAsync(new Exception());
            var errorHandlerMock = new Mock<IErrorHandler>();
            errorHandlerMock.Setup(handler => handler.HandleAsync(It.IsAny<IContext>(), It.IsAny<Exception>()))
                            .ThrowsAsync(new Exception());
            using (var client = new Bootstrap().AddMessageHandler(messageHandlerMock.Object)
                                               .AddErrorHandler(errorHandlerMock.Object)
                                               .Configure(options =>
                                               {
                                                   options.ListeningPoint = firstEndPoint;
                                               })
                                               .Build())
            {
                await client.SendAsync(message);
                await Task.Delay(timeout);
                await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendAsync(message));
                Assert.Throws<AggregateException>(() => client.Stop());
            }

            using (var client = new Bootstrap().Configure(options =>
                                               {
                                                   options.ListeningPoint = firstEndPoint;
                                               })
                                               .Build())
            {
                await client.SendAsync(message);
                client.Stop();
                await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendAsync(message));
            }
        }
    }
}
