using Datagrammer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Tests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void CreateHosted_WithoutCustomServices_AllDependenciesAreResolved()
        {
            var provider = new ServiceCollection().AddHostedDatagrammer().BuildServiceProvider();

            var client = provider.GetService<IDatagramClient>();
            var sender = provider.GetService<IDatagramSender>();
            var hostedService = provider.GetService<IHostedService>();

            Assert.NotNull(client);
            Assert.NotNull(sender);
            Assert.NotNull(hostedService);
        }

        [Fact]
        public void BuildInstance_WithoutCustomServices_NotNull()
        {
            var client = new ServiceCollection().BuildDatagramClient();

            Assert.NotNull(client);
        }
    }
}
