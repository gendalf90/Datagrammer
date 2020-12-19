using System;

namespace Datagrammer
{
    public readonly struct Try<T>
    {
        public Try(T value)
        {
            Value = value;
            Exception = null;
        }

        public Try(Exception exception)
        {
            Value = default;
            Exception = exception;
        }

        public T Value { get; }

        public Exception Exception { get; }
    }
}
