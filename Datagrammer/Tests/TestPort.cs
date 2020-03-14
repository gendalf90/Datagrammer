using System.Threading;

namespace Tests
{
    public static class TestPort
    {
        private static int initialPort = 50000;

        public static int GetNext()
        {
            return Interlocked.Increment(ref initialPort);
        }
    }
}
