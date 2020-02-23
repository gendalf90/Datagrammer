﻿using System.Threading;
using System.Threading.Tasks;

namespace Datagrammer.Middleware
{
    public sealed class MiddlewareOptions
    {
        public TaskScheduler TaskScheduler { get; set; } = TaskScheduler.Default;

        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public int InputBufferCapacity { get; set; } = 1;

        public int ProcessingParallelismDegree { get; set; } = 1;

        public int OutputBufferCapacity { get; set; } = 1;
    }
}
