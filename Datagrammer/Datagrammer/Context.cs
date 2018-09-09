using System.Threading.Tasks;

namespace Datagrammer
{
    internal class Context : IContext
    {
        private readonly IDatagramSender sender;

        public Context(IDatagramSender sender)
        {
            this.sender = sender;
        }

        public Task SendAsync(Datagram message)
        {
            return sender.SendAsync(message);
        }
    }
}
