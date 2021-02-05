using System.Collections.Generic;
using System.Threading;

namespace Datagrammer.AsyncEnumerables
{
    internal sealed class InputAsyncEnumerable : IAsyncEnumerable<AsyncEnumeratorContext>
    {
        private readonly IDatagramSocket socket;

        public InputAsyncEnumerable(IDatagramSocket socket)
        {
            this.socket = socket;
        }

        public IAsyncEnumerator<AsyncEnumeratorContext> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new InputAsyncEnumerator(socket, cancellationToken);
        }
    }
}
