using Datagrammer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Tests.Intergration
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void CreateHosted_WithoutCustomServices_AllDependenciesAreResolved()
        {
            var provider = new ServiceCollection().AddHostedDatagrammer().BuildServiceProvider();

            var sender = provider.GetService<IDatagramSender>();
            var hostedService = provider.GetService<IHostedService>();

            Assert.NotNull(sender);
            Assert.NotNull(hostedService);
        }
    }
}
