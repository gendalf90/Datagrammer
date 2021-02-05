using System.Collections.Generic;
using System.Threading;

namespace Datagrammer.AsyncEnumerables
{
    internal sealed class OutputAsyncEnumerable : IAsyncEnumerable<AsyncEnumeratorContext>
    {
        private readonly IDatagramSocket socket;

        public OutputAsyncEnumerable(IDatagramSocket socket)
        {
            this.socket = socket;
        }

        public IAsyncEnumerator<AsyncEnumeratorContext> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new OutputAsyncEnumerator(socket, cancellationToken);
        }
    }
}
