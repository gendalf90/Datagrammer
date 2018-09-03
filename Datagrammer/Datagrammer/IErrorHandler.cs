using System;
using System.Threading.Tasks;

namespace Datagrammer
{
    public interface IErrorHandler
    {
        Task HandleAsync(Exception e);
    }
}
