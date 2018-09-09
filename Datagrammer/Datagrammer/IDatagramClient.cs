using System;

namespace Datagrammer
{
    public interface IDatagramClient : IDatagramSender, IDisposable
    {
        void Stop();
    }
}
