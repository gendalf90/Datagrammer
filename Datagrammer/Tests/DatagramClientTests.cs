using Xunit;
using Moq;
using Datagrammer;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System;

namespace Tests
{
    public class DatagramClientTests
    {
        //[Fact]
        //public async Task MessageWasReceived_WithCustomMessageHandler_HandlerIsCalledWithExpectedMessage()
        //{
        //    var messageHandlerMock = new Mock<IMessageHandler>();
        //    var protocolMock = new Mock<IProtocol>();
        //    protocolMock.SetupSequence(mock => mock.ReceiveAsync())
        //                .ReturnsAsync(new Datagram())
        //                .Returns(InfiniteResultWaitingAsync<Datagram>);
        //    var protocolCreatorMock = new Mock<IProtocolCreator>();
        //    protocolCreatorMock.SetReturnsDefault(protocolMock.Object);
        //    var client = new ServiceCollection().AddSingleton(messageHandlerMock.Object)
        //                                        .AddSingleton(protocolCreatorMock.Object)
        //                                        .BuildDatagramClient();

        //    await client.StartAsync(CancellationToken.None);
            
        //}

        //private async Task<T> InfiniteResultWaitingAsync<T>()
        //{
        //    await Task.Delay(Timeout.InfiniteTimeSpan);
        //    return default(T);
        //}
    }
}
