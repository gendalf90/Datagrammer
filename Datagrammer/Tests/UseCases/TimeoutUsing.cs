using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Xunit;
using Datagrammer.Timeout;
using FluentAssertions;

namespace Tests.UseCases
{
    public class TimeoutUsing
    {
        [Fact(DisplayName = "timeout when new messages are not sent too long")]
        public async Task CaseOne()
        {
            var timeoutBufferBlock = new TimeoutBufferBlock<int>(TimeSpan.FromSeconds(1));

            await Task.Delay(TimeSpan.FromSeconds(2));

            timeoutBufferBlock
                .Awaiting(block => block.Completion)
                .Should()
                .Throw<TimeoutException>();
        }

        [Fact(DisplayName = "no timeout if messages are sending")]
        public async Task CaseTwo()
        {
            var timeoutBufferBlock = new TimeoutBufferBlock<int>(TimeSpan.FromSeconds(3), new TimeoutOptions
            {
                InputBufferCapacity = 3
            });

            timeoutBufferBlock.LinkTo(DataflowBlock.NullTarget<int>());

            await Task.Delay(TimeSpan.FromSeconds(1));

            await timeoutBufferBlock.SendAsync(1);

            await Task.Delay(TimeSpan.FromSeconds(2));

            await timeoutBufferBlock.SendAsync(2);

            await Task.Delay(TimeSpan.FromSeconds(2));

            timeoutBufferBlock.Complete();

            await Task.Delay(TimeSpan.FromSeconds(3));

            timeoutBufferBlock
                .Awaiting(block => block.Completion)
                .Should()
                .NotThrow();
        }

        [Fact(DisplayName = "timeout when new messages are not sent too long (with extensions)")]
        public async Task CaseThree()
        {
            var bufferBlock = new BufferBlock<int>();

            var timeoutDecorator = bufferBlock.WithTimeout(TimeSpan.FromSeconds(1));

            await Task.Delay(TimeSpan.FromSeconds(2));

            timeoutDecorator
                .Awaiting(block => block.Completion)
                .Should()
                .Throw<TimeoutException>();
            bufferBlock
                .Awaiting(block => block.Completion)
                .Should()
                .Throw<TimeoutException>();
        }
    }
}
