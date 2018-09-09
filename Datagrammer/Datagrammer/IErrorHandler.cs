using System;
using System.Threading.Tasks;

namespace Datagrammer
{
    public interface IErrorHandler
    {
        Task HandleAsync(IContext context, Exception e);
    }
}
