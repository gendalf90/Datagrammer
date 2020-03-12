using System;
using System.Threading.Tasks;

namespace Datagrammer.Middleware
{
    public sealed class ActionMiddlewareBlock<TInput, TOutput> : MiddlewareBlock<TInput, TOutput>
    {
        private readonly Func<TInput, Func<TOutput, Task>, Task> middleware;

        public ActionMiddlewareBlock(Func<TInput, Func<TOutput, Task>, Task> middleware, MiddlewareOptions options) : base(options)
        {
            this.middleware = middleware ?? throw new ArgumentNullException(nameof(middleware));
        }

        protected override async Task ProcessAsync(TInput value)
        {
            await middleware(value, NextAsync);
        }
    }
}
