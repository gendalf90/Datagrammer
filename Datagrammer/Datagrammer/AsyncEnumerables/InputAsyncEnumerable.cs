using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Datagrammer.AsyncEnumerables
{
    internal sealed class InputAsyncEnumerable : IAsyncEnumerable<AsyncEnumeratorContext>
    {
        private readonly Socket socket;

        public InputAsyncEnumerable(Socket socket)
        {
            this.socket = socket;
        }

        public IAsyncEnumerator<AsyncEnumeratorContext> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new InputAsyncEnumerator(socket, cancellationToken);
        }
    }
}
