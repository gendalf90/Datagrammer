namespace Datagrammer.Middleware
{
    public sealed class MiddlewareOptions
    {
        public int InputBufferCapacity { get; set; } = 1;

        public int ProcessingParallelismDegree { get; set; } = 1;

        public int OutputBufferCapacity { get; set; } = 1;
    }
}
