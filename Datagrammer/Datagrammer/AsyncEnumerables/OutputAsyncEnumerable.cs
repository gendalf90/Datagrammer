using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Datagrammer.AsyncEnumerables
{
    internal sealed class OutputAsyncEnumerable : IAsyncEnumerable<AsyncEnumeratorContext>
    {
        private readonly Socket socket;

        public OutputAsyncEnumerable(Socket socket)
        {
            this.socket = socket;
        }

        public IAsyncEnumerator<AsyncEnumeratorContext> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new OutputAsyncEnumerator(socket, cancellationToken);
        }
    }
}
