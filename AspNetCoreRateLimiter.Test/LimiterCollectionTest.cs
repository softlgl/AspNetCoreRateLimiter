using AspNetCoreRateLimiter;
using RateLimiterCore.LimiterService;
using Xunit;

namespace AspNetCoreRateLimiter.Test
{
    public class LimiterCollectionTest
    {
        [Fact]
        public void Add_Get_与索引器()
        {
            var service = new StubLimiterService(true);
            var collection = new LimiterCollection();
            collection.Add("/api", service);

            Assert.Contains("/api", collection.AllPath);
            Assert.Same(service, collection.Get("/api"));
            Assert.Same(service, collection["/api"]);
        }
    }
}