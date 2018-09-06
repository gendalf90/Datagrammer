using System.Net;

namespace Datagrammer
{
    internal class Options
    {
        public int ReceivingParallelismDegree { get; set; }

        public IPEndPoint ListeningPoint { get; set; }
    }
}
