using System.Net;

namespace Datagrammer
{
    public interface IProtocolCreator
    {
        IProtocol Create(IPEndPoint listeningPoint);
    }
}
