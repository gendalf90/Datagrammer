using System.Net;

namespace Datagrammer
{
    internal interface IMessageClientCreator
    {
        IMessageClient Create(IPEndPoint listeningPoint);
    }
}
