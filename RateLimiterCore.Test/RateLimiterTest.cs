using System;
using System.Diagnostics;
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
            var limiter = RateLimiter.Create(type, 4, limitSize);

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

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 突发用尽后_立即拒绝_额外请求(LimiterType type)
        {
            const int limitSize = 5;
            var limiter = RateLimiter.Create(type, 4, limitSize);

            for (int i = 0; i < limitSize; i++)
            {
                limiter.Acquire();
            }

            // 突发已用尽，不等待的情况下下一个请求应被拒绝
            Assert.False(limiter.Acquire());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(3600)]
        public void 漏桶的_桶容量_不影响其严格匀速行为(int limitSize)
        {
            var limiter = RateLimiter.Create(LimiterType.LeakageBucket, 10, limitSize);

            // 严格匀速下突发窗口恒为 0，容量无论多大同一瞬间都只放行一个
            Assert.True(limiter.Acquire());
            Assert.False(limiter.Acquire());
        }

        #endregion

        #region 速率恢复

        [Theory]
        [InlineData(1)] // 闲置约 4 个周期，接近容量
        [InlineData(3)] // 闲置约 12 个周期，远超容量
        public void 长时间闲置后_突发最多攒满桶容量(int idleSeconds)
        {
            const int maxQPS = 4;
            const int limitSize = 5;
            var limiter = RateLimiter.Create(LimiterType.TokenBucket, maxQPS, limitSize);

            // 闲置期间水位冻结、now 持续前进，两者差距会越拉越大
            Thread.Sleep(idleSeconds * 1000);

            int success = 0;
            for (int i = 0; i < limitSize * 4; i++)
            {
                if (limiter.Acquire())
                {
                    success++;
                }
            }

            // 差距再大也只能攒满一个桶，绝不会放行"闲置时长 × 速率"那么多
            Assert.Equal(limitSize, success);
        }

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 等待一个周期后_恢复并重新放行(LimiterType type)
        {
            const int maxQPS = 4;
            const int limitSize = 5;
            var limiter = RateLimiter.Create(type, maxQPS, limitSize);

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

        #endregion

        #region 批量扣减

        [Fact]
        public void Acquire_按permits_扣减对应额度()
        {
            // 用 1 QPS（周期 1 秒）让断言不依赖测试机的执行耗时
            var limiter = RateLimiter.Create(LimiterType.TokenBucket, 1, 4);

            Assert.True(limiter.Acquire(3));
            // 已消耗 3 个额度，容量 4 只剩 1 个：再要 2 个应被拒，避免超发
            Assert.False(limiter.Acquire(2));
            Assert.True(limiter.Acquire(1));
        }

        [Fact]
        public void 漏桶下_批量扣减_同样受匀速约束()
        {
            // 漏桶水位每次重置到当前时间、不累积欠账，批量放行后必须等满对应周期
            var limiter = RateLimiter.Create(LimiterType.LeakageBucket, 1, 100);

            Assert.True(limiter.Acquire(3));
            // 放行后水位推到 now + 3 个周期，未等待前不得再次放行
            Assert.False(limiter.Acquire(1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Acquire_permits非正数_抛出异常(int permits)
        {
            var limiter = RateLimiter.Create(LimiterType.TokenBucket, 4, 5);
            Assert.Throws<ArgumentOutOfRangeException>(() => limiter.Acquire(permits));
        }

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void Acquire_permits超过桶容量_抛出异常(LimiterType type)
        {
            const int limitSize = 5;
            var limiter = RateLimiter.Create(type, 4, limitSize);
            // 一次索取超过整个桶的额度没有意义，且会让步进失去上界
            Assert.Throws<ArgumentOutOfRangeException>(() => limiter.Acquire(limitSize + 1));
        }

        [Fact]
        public void Acquire_permits为一_与无参调用等价()
        {
            // 容量为 5 的令牌桶：permits=1 时应恰好放行 5 次，与 permits=1 前的语义完全一致
            var limiter = RateLimiter.Create(LimiterType.TokenBucket, 1, 5);

            for (int i = 0; i < 5; i++)
            {
                Assert.True(limiter.Acquire(1));
            }
            Assert.False(limiter.Acquire(1));
        }

        #endregion

        #region 重试间隔

        [Fact]
        public void TimeUntilNextSlot_可放行时返回零()
        {
            var limiter = RateLimiter.Create(LimiterType.TokenBucket, 4, 5);
            Assert.Equal(TimeSpan.Zero, limiter.TimeUntilNextSlot());
        }

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void TimeUntilNextSlot_突发用尽后_返回约一个周期(LimiterType type)
        {
            const int maxQPS = 4;
            const int limitSize = 5;
            var limiter = RateLimiter.Create(type, maxQPS, limitSize);

            for (int i = 0; i < limitSize; i++)
            {
                limiter.Acquire();
            }
            Assert.False(limiter.Acquire());

            TimeSpan wait = limiter.TimeUntilNextSlot();
            // 恢复一个额度需等待一个周期 = 1/maxQPS 秒，给少量容差
            Assert.InRange(wait.TotalMilliseconds, 0, 1000.0 / maxQPS + 50);
        }

        [Fact]
        public void TimeUntilNextSlot_等待足量时间后归零()
        {
            const int maxQPS = 100; // 一个周期 = 10ms
            var limiter = RateLimiter.Create(LimiterType.LeakageBucket, maxQPS, 5);

            Assert.True(limiter.Acquire());
            Assert.True(limiter.TimeUntilNextSlot() > TimeSpan.Zero);

            Thread.Sleep(50); // 远超一个周期
            Assert.Equal(TimeSpan.Zero, limiter.TimeUntilNextSlot());
        }

        #endregion

        #region 参数校验

        [Fact]
        public void 非正参数会被钳制为默认值()
        {
            var limiter = new TokenBucketLimiterService(0, 0);
            Assert.Equal(1, limiter.MaxQPS);
            Assert.Equal(50, limiter.LimitSize);
        }

        [Fact]
        public void 配置值可通过公开属性读取()
        {
            var limiter = new TokenBucketLimiterService(7, 13);
            Assert.Equal(7, limiter.MaxQPS);
            Assert.Equal(13, limiter.LimitSize);
        }

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void maxQPS超过计时器频率_抛出异常(LimiterType type)
        {
            long overLimit = Stopwatch.Frequency + 1;
            Assert.Throws<ArgumentOutOfRangeException>(() => RateLimiter.Create(type, (int)overLimit, 5));
        }

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void limitSize过大导致突发窗口超一小时_抛出异常(LimiterType type)
        {
            // maxQPS = 1 时容量上限 = 3600，超过即意味着一次突发可放行一小时的流量
            Assert.Throws<ArgumentOutOfRangeException>(() => RateLimiter.Create(type, 1, 3601));
        }

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 容量上限随速率放大(LimiterType type)
        {
            // maxQPS = 10 时上限 = 36000，边界值应可正常构造
            var limiter = RateLimiter.Create(type, 10, 36000);
            Assert.Equal(10, limiter.MaxQPS);
            Assert.Equal(36000, limiter.LimitSize);
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

        #region 并发

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 高度并发下_放行总数不超过容量加容差(LimiterType type)
        {
            const int maxQPS = 100;
            const int limitSize = 20;
            var limiter = RateLimiter.Create(type, maxQPS, limitSize);

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

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 高度并发下_批量扣减_放行额度不超过容量加容差(LimiterType type)
        {
            const int maxQPS = 100;
            const int limitSize = 20;
            const int permits = 3;
            var limiter = RateLimiter.Create(type, maxQPS, limitSize);

            int acquired = 0;
            var threads = new Thread[200];
            for (int t = 0; t < threads.Length; t++)
            {
                threads[t] = new Thread(() =>
                {
                    if (limiter.Acquire(permits))
                    {
                        Interlocked.Add(ref acquired, permits);
                    }
                });
            }
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();

            // 批量扣减不得突破容量：否则 permits 越大越能绕过限流
            Assert.True(acquired <= limitSize + permits, $"放行额度 {acquired} 远超容量 {limitSize}");
        }

        #endregion

        #region 时钟回跳自愈

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 时钟回跳导致水位异常领先时_自愈而非永久拒绝(LimiterType type)
        {
            var limiter = RateLimiter.Create(type, 100, 20);

            // 模拟时钟回跳：把水位推到远超任何合法调用所能产生的未来（约 1 小时）。
            // 不自愈的话，要等 now 重新追上水位才能放行，期间所有请求都会被拒。
            SetWatermark(limiter, Stopwatch.GetTimestamp() + Stopwatch.Frequency * 3600);

            Assert.True(limiter.Acquire());

            // 自愈后等待时间回到正常量级，而不是"再等一小时"
            Assert.True(limiter.TimeUntilNextSlot() < TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void 自愈是拉回水位_而非清空限流状态()
        {
            const int limitSize = 5;
            var limiter = RateLimiter.Create(LimiterType.TokenBucket, 1, limitSize);

            SetWatermark(limiter, Stopwatch.GetTimestamp() + Stopwatch.Frequency * 3600);

            Assert.True(limiter.Acquire()); // 这一次触发自愈

            // 自愈只把水位拉回当前时间，突发额度仍受桶容量约束
            int success = 1;
            for (int i = 0; i < limitSize * 3; i++)
            {
                if (limiter.Acquire())
                {
                    success++;
                }
            }
            Assert.Equal(limitSize, success);
        }

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void 高并发下_变化的permits_不会被误判为时钟回跳(LimiterType type)
        {
            const int maxQPS = 100;
            const int limitSize = 20;
            var limiter = RateLimiter.Create(type, maxQPS, limitSize);

            int acquired = 0;
            var threads = new Thread[100];
            for (int t = 0; t < threads.Length; t++)
            {
                int permits = (t % limitSize) + 1; // 覆盖到最大步进
                threads[t] = new Thread(() =>
                {
                    if (limiter.Acquire(permits))
                    {
                        Interlocked.Add(ref acquired, permits);
                    }
                });
            }
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();

            // 一旦把正常的高水位误判成回跳，水位会被反复重置、限流形同虚设，
            // 放行额度会飙升到远超容量（全部 100 个线程都成功）
            Assert.True(acquired <= limitSize + 20, $"放行额度 {acquired} 远超容量 {limitSize}，疑似误判自愈");
        }

        [Theory]
        [InlineData(LimiterType.TokenBucket)]
        [InlineData(LimiterType.LeakageBucket)]
        public void TimeUntilNextSlot_时钟回跳时_返回值被钳制(LimiterType type)
        {
            var limiter = RateLimiter.Create(type, 100, 20);

            // 极端回跳：水位被推到近乎 long 上界
            SetWatermark(limiter, long.MaxValue / 2);

            TimeSpan wait = limiter.TimeUntilNextSlot();

            // 不钳制的话这里会是天文数字，换算成秒后足以让 Retry-After 溢出成负数
            Assert.True(wait.TotalSeconds < 60, $"回跳期间的重试间隔应被钳制，实际为 {wait}");
        }

        #endregion

        /// <summary>直接改写水位，用于模拟时钟回跳这一无法在进程内真实触发的场景。</summary>
        private static void SetWatermark(ILimiterService limiter, long ticks)
        {
            var field = limiter.GetType().BaseType
                .GetField("_watermarkTicks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            field.SetValue(limiter, ticks);
        }
    }
}
