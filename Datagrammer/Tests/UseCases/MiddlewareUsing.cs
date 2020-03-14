using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Xunit;
using Datagrammer.Middleware;
using FluentAssertions;
using System.Collections.Generic;

namespace Tests.UseCases
{
    public class MiddlewareUsing
    {
        class TestMiddlewareBlock : MiddlewareBlock<int, string>
        {
            public TestMiddlewareBlock(MiddlewareOptions options) : base(options)
            {
            }

            protected override async Task ProcessAsync(int value)
            {
                await NextAsync(value.ToString()); //send data to next block. you can modify it before. 
                                                   //or you can just send current data and process it below. 
                                                   //don't call the method if you want to break current request pipeline.
                                                   //it just puts the data to an internal source buffer, all middlewares work in parallel

            }
        }

        [Fact(DisplayName = "simple modification middleware")]
        public async Task CaseOne()
        {
            var buffer = new BufferBlock<string>();

            var middleware = new TestMiddlewareBlock(new MiddlewareOptions());

            middleware.LinkTo(buffer, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });

            await middleware.SendAsync(1);

            var result = await buffer.ReceiveAsync();

            middleware.Complete();

            await buffer.Completion;

            result.Should().Be("1");
        }

        [Fact(DisplayName = "simple aggregation chain")]
        public async Task CaseTwo()
        {
            var mainAction = new TransformBlock<string, string>(value => value + " three");

            var afterChain = mainAction
                .UseAfter<string, string>(async (value, next) => await next(value + " four"))
                .UseAfter<string, string>(async (value, next) => await next(value + " five"));

            var beforeChain = mainAction
                .UseBefore<string, string>(async (value, next) => await next(value + " two"))
                .UseBefore<string, string>(async (value, next) => await next(value + " one"));

            await beforeChain.SendAsync("zero");

            var result = await afterChain.ReceiveAsync();

            beforeChain.Complete();

            await afterChain.Completion;

            result.Should().Be("zero one two three four five");
        }

        [Fact(DisplayName = "simple filtering chain")]
        public async Task CaseThree()
        {
            var buffer = new BufferBlock<int>();

            var afterChain = buffer
                .UseAfter<int, int>(async (value, next) =>
                {
                    if (value != 4)
                    {
                        await next(value);
                    }
                });

            var beforeChain = buffer
                .UseBefore<int, int>(async (value, next) =>
                {
                    if (value != 2)
                    {
                        await next(value);
                    }
                });

            for(int i = 0; i < 6; i++)
            {
                await beforeChain.SendAsync(i);
            }

            var results = new List<int>();

            for (int i = 0; i < 4; i++)
            {
                var result = await afterChain.ReceiveAsync();

                results.Add(result);
            }

            beforeChain.Complete();

            await afterChain.Completion;

            results.Should().BeEquivalentTo(new int[] { 0, 1, 3, 5 });
        }

        [Fact(DisplayName = "simple transformation chain")]
        public async Task CaseFour()
        {
            var chain = new TransformBlock<string, int>(value => 3)
                .UseBefore<object, string, int>(async (value, next) => await next("2")) //value is object, result is string
                .UseAfter<object, int, string>(async (value, next) => await next("4")) //value is int, result is string
                .UseBefore<bool, object, string>(async (value, next) => await next(1)) //value is bool, result is object
                .UseAfter<bool, string, int>(async (value, next) => await next(5)); //value is string, result is int

            await chain.SendAsync(true);

            var result = await chain.ReceiveAsync();

            chain.Complete();

            await chain.Completion;

            result.Should().Be(5);
        }
    }
}
