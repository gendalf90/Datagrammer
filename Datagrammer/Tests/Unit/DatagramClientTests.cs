using Xunit;
using Moq;
using Datagrammer;
using System.Net;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Tests.Unit
{
    public class DatagramClientTests
    {
        [Fact]
        public void MessageWasReceived_WithCustomMessageHandler_HandlerIsCalledWithExpectedMessage()
        {
            var sended = new Datagram { Bytes = new byte[] { 1, 2, 3 }, EndPoint = new IPEndPoint(IPAddress.Loopback, 12345) };
            var received = new List<Datagram>();
            var messageHandlerMock = new Mock<IMessageHandler>();
            messageHandlerMock.Setup(handler => handler.HandleAsync(It.IsAny<IContext>(), It.IsAny<Datagram>()))
                              .Callback<IContext, Datagram>((context, message) => received.Add(message));
            var protocolMock = new Mock<IProtocol>();
            protocolMock.SetupSequence(mock => mock.ReceiveAsync())
                        .ReturnsAsync(sended)
                        .ThrowsAsync(new ObjectDisposedException("DatagramClient"));
            var protocolCreatorMock = new Mock<IProtocolCreator>();
            protocolCreatorMock.SetReturnsDefault(protocolMock.Object);

            var client = new Bootstrap().AddMessageHandler(messageHandlerMock.Object)
                                        .UseCustomProtocol(protocolCreatorMock.Object)
                                        .Build();

            Assert.NotEmpty(received);
            Assert.Contains(received, message => message.Bytes.SequenceEqual(sended.Bytes) && message.EndPoint.Equals(sended.EndPoint));
        }
    }
}
