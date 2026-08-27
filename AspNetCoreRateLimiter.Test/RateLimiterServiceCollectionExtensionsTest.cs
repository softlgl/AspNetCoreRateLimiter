using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RateLimiterCore;
using Xunit;

namespace AspNetCoreRateLimiter.Test
{
    public class RateLimiterServiceCollectionExtensionsTest
    {
        [Fact]
        public void 单个选项_注册单例集合()
        {
            var services = new ServiceCollection();
            services.AddRateLimiter(new RateLimiterOptions
            {
                Path = "/api",
                LimiterType = LimiterType.TokenBucket,
                MaxQPS = 3,
                LimitSize = 5
            });

            using (var provider = services.BuildServiceProvider())
            {
                var collection = provider.GetService<LimiterCollection>();
                Assert.NotNull(collection);
                Assert.Contains("/api", collection.AllPath);

                // 单例：重复解析应为同一实例
                Assert.Same(collection, provider.GetService<LimiterCollection>());
            }
        }

        [Fact]
        public void 多个选项_注册多条路径()
        {
            var services = new ServiceCollection();
            services.AddRateLimiter(new List<RateLimiterOptions>
            {
                new RateLimiterOptions { Path = "/api/a", LimiterType = LimiterType.TokenBucket, MaxQPS = 3, LimitSize = 5 },
                new RateLimiterOptions { Path = "/api/b", LimiterType = LimiterType.LeakageBucket, MaxQPS = 4, LimitSize = 6 }
            });

            using (var provider = services.BuildServiceProvider())
            {
                var collection = provider.GetService<LimiterCollection>();
                Assert.NotNull(collection);
                Assert.Equal(2, new List<string>(collection.AllPath).Count);
                Assert.Contains("/api/a", collection.AllPath);
                Assert.Contains("/api/b", collection.AllPath);
            }
        }

        [Fact]
        public void 配置文件_读取RateLimiterOptions节()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["0:Path"] = "/api",
                    ["0:LimiterType"] = "TokenBucket",
                    ["0:MaxQPS"] = "10",
                    ["0:LimitSize"] = "20"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddRateLimiter(configuration);

            using (var provider = services.BuildServiceProvider())
            {
                var collection = provider.GetService<LimiterCollection>();
                Assert.NotNull(collection);
                Assert.Contains("/api", collection.AllPath);
            }
        }

        [Fact]
        public void 配置文件_无配置项_抛出异常()
        {
            var configuration = new ConfigurationBuilder().Build();

            var services = new ServiceCollection();
            var exception = Assert.Throws<ArgumentNullException>(() => services.AddRateLimiter(configuration));
            Assert.Contains("RateLimiterOptions", exception.Message);
        }
    }
}