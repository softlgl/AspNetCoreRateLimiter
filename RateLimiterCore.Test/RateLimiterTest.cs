using System;
using System.Reflection;
using System.Threading;
using RateLimiterCore.LimiterService;
using Xunit;

namespace RateLimiterCore.Test
{
    public class RateLimiterTest
    {
        #region 突发容量

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 允许_启动即放行_限流容量的_突发请求(LimiterType type)
        {
            const int limitSize = 5;
            using (var limiter = RateLimiter.Create(type, 4, limitSize))
            {
                int success = 0;
                for (int i = 0; i < limitSize; i++)
                {
                    if (limiter.Acquire())
                    {
                        success++;
                    }
                }
                // 令牌桶允许突发：启动可放行整个容量；漏桶严格匀速：同一瞬间只放行一个
                int expected = type == LimiterType.TokenBucket ? limitSize : 1;
                Assert.Equal(expected, success);
            }
        }

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 突发用尽后_立即拒绝_额外请求(LimiterType type)
        {
            const int limitSize = 5;
            using (var limiter = RateLimiter.Create(type, 4, limitSize))
            {
                for (int i = 0; i < limitSize; i++)
                {
                    limiter.Acquire();
                }
                // 突发已用尽，不等待的情况下下一个请求应被拒绝
                Assert.False(limiter.Acquire());
            }
        }

        #endregion

        #region 速率恢复

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 等待一个周期后_恢复并重新放行(LimiterType type)
        {
            const int maxQPS = 4;
            const int limitSize = 5;
            using (var limiter = RateLimiter.Create(type, maxQPS, limitSize))
            {
                for (int i = 0; i < limitSize; i++)
                {
                    limiter.Acquire();
                }
                Assert.False(limiter.Acquire());

                // 等待 1 秒后应恢复约 maxQPS 个令牌（给少量容差避免 CI 抖动）
                Thread.Sleep(1000);
                int success = 0;
                for (int i = 0; i < maxQPS + 2; i++)
                {
                    if (limiter.Acquire())
                    {
                        success++;
                    }
                }
                if (type == LimiterType.TokenBucket)
                {
                    // 令牌桶：闲置攒突发，等待 1 秒后恢复约 maxQPS 个（给少量容差避免 CI 抖动）
                    Assert.InRange(success, maxQPS - 1, maxQPS + 1);
                }
                else
                {
                    // 漏桶严格匀速：回环内不等待，同一瞬间只允许放行一个
                    Assert.Equal(1, success);
                }
            }
        }

        #endregion

        #region 参数默认值

        [Fact]
        public void 非正参数会被钳制为默认值()
        {
            using (var limiter = new TokenBucketLimiterService(0, 0))
            {
                Assert.Equal(1, ReadField<int>(limiter, "_maxQPS"));
                Assert.Equal(50, ReadField<int>(limiter, "_limitSize"));
            }
        }

        #endregion

        #region 工厂

        [Fact]
        public void Create_按类型返回对应实现()
        {
            Assert.IsType<TokenBucketLimiterService>(RateLimiter.Create(LimiterType.TokenBucket, 3, 5));
            Assert.IsType<LeakageBucketLimiterService>(RateLimiter.Create(LimiterType.LeakageBucket, 3, 5));
        }

        [Fact]
        public void Create_未知类型_默认回退到令牌桶()
        {
            Assert.IsType<TokenBucketLimiterService>(RateLimiter.Create((LimiterType)99, 3, 5));
        }

        #endregion

        #region Dispose

        [Fact]
        public void Dispose_可以安全多次调用()
        {
            var limiter = RateLimiter.Create(LimiterType.TokenBucket, 3, 5);
            limiter.Dispose();
            limiter.Dispose(); // 应为幂等且不抛异常
        }

        #endregion

        #region 并发

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 高度并发下_放行总数不超过容量加容差(LimiterType type)
        {
            const int maxQPS = 100;
            const int limitSize = 20;
            using (var limiter = RateLimiter.Create(type, maxQPS, limitSize))
            {
                int success = 0;
                var threads = new Thread[200];
                for (int t = 0; t < threads.Length; t++)
                {
                    threads[t] = new Thread(() =>
                    {
                        if (limiter.Acquire())
                        {
                            Interlocked.Increment(ref success);
                        }
                    });
                }
                foreach (var t in threads) t.Start();
                foreach (var t in threads) t.Join();

                Assert.True(success > 0);
                // 几乎同时发生的并发请求，放行数不应明显超出桶容量
                Assert.True(success <= limitSize + 5, $"放行 {success} 远超容量 {limitSize}");
            }
        }

        #endregion

        private static T ReadField<T>(object target, string fieldName)
        {
            var field = target.GetType().BaseType
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field.GetValue(target);
        }
    }
}