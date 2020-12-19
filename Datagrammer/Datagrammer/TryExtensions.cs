namespace Datagrammer
{
    public static class TryExtensions
    {
        public static bool IsException<T>(this Try<T> tryable)
        {
            return tryable.Exception != null;
        }

        public static bool IsValue<T>(this Try<T> tryable)
        {
            return tryable.Exception == null;
        }
    }
}
