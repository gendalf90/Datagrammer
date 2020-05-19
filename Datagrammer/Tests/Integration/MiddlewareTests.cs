using Datagrammer.Dataflow.Middleware;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Xunit;

namespace Tests.Integration
{
    public class MiddlewareTests
    {
        [Fact]
        public void Complete_IsCompleted()
        {
            //Arrange
            var middleware = new ActionMiddlewareBlock<int, int>(async (value, next) =>
            {
                await next(value);
            }, new MiddlewareOptions());

            //Act
            middleware.Complete();

            //Assert
            middleware
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
        }

        [Fact]
        public void Fault_IsFaulted()
        {
            //Arrange
            var middleware = new ActionMiddlewareBlock<int, int>(async (value, next) =>
            {
                await next(value);
            }, new MiddlewareOptions());

            //Act
            middleware.Fault(new ArgumentException());

            //Assert
            middleware
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<ArgumentException>();
        }

        [Fact]
        public void Cancel_IsCanceled()
        {
            //Arrange
            var source = new CancellationTokenSource();
            var middleware = new ActionMiddlewareBlock<int, int>(async (value, next) =>
            {
                await next(value);
            }, new MiddlewareOptions
            {
                CancellationToken = source.Token
            });

            //Act
            source.Cancel();

            //Assert
            middleware
                .Awaiting(reader => reader.Completion)
                .Should()
                .Throw<OperationCanceledException>();
        }

        [Fact]
        public void SendMessages_MessagesAreReceived()
        {
            //Arrange
            var middleware = new ActionMiddlewareBlock<int, int>(async (value, next) =>
            {
                await next(value);
            }, new MiddlewareOptions
            {
                InputBufferCapacity = 3
            });
            var receivedMessages = new List<int>();

            //Act
            for(int i = 0; i < 3; i++)
            {
                middleware.Post(i);
            }

            middleware.Complete();

            for (int i = 0; i < 3; i++)
            {
                receivedMessages.Add(middleware.Receive());
            }

            //Assert
            middleware
                .Awaiting(reader => reader.Completion)
                .Should()
                .NotThrow();
            receivedMessages
                .Should()
                .BeEquivalentTo(new int[] { 0, 1, 2 });
        }
    }
}
