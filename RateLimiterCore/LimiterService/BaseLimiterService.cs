using System;
using System.Diagnostics;
using System.Threading;

namespace RateLimiterCore.LimiterService
{
    /// <summary>
    /// 限流基类：基于时间戳懒计算（无后台线程、无锁、无队列）。
    ///
    /// 原理（GCRA 思路）：用"水位时间戳"表示账务基准——
    /// 每放行一次请求，水位推后一个令牌周期（1/maxQPS 秒）；
    /// 时间的流逝会自动"偿还欠账"（水位相对当前时间自然回落）。
    /// 当水位超出当前时间一个突发窗口（BurstTicks）时拒绝请求。
    ///
    /// 两个可覆盖点让子类决定自身的过载表现：
    ///  1. BurstTicks         —— 允许容忍多少"超出当前时间的欠账"（突发头寸）；
    ///  2. AdvanceWatermark   —— 放行后如何推进水位（是否允许空闲时"攒突发"）。
    /// 默认实现 = 允许突发（令牌桶语义）；漏桶覆盖为严格匀速。
    /// </summary>
    public abstract class BaseLimiterService : ILimiterService
    {
        protected readonly int _maxQPS;
        protected readonly int _limitSize;
        // 每个令牌周期对应的 Stopwatch ticks 数（两次放行的最小间隔）
        protected readonly long _periodTicks;
        // 突发窗口（ticks）：容忍水位超出当前时间的上限。令牌桶 = (容量-1) 个周期，漏桶 = 0
        protected virtual long BurstTicks => (_limitSize - 1) * _periodTicks;
        // 放行后的新水位：默认累加一个周期（允许空闲攒突发）；漏桶覆盖为钳制到当前时间再推后
        protected virtual long AdvanceWatermark(long watermark, long now) => watermark + _periodTicks;
        // 水位时间戳，CAS 更新
        private long _watermarkTicks;

        protected BaseLimiterService(int maxQPS, int limitSize)
        {
            _maxQPS = maxQPS > 0 ? maxQPS : 1;
            _limitSize = limitSize > 0 ? limitSize : 50;
            _periodTicks = Math.Max(1L, (long)Math.Round((double)Stopwatch.Frequency / _maxQPS));
            // 初始水位设为当前时间：令牌桶由此获得 limitSize 次初始突发
            _watermarkTicks = Stopwatch.GetTimestamp();
        }

        public bool Acquire()
        {
            long burst = BurstTicks;
            while (true)
            {
                long watermark = Volatile.Read(ref _watermarkTicks);
                long now = Stopwatch.GetTimestamp();

                // 欠账超过突发窗口，拒绝
                if (watermark - now > burst)
                {
                    return false;
                }

                // 放行：推进水位；并发竞争失败则重试
                long next = AdvanceWatermark(watermark, now);
                if (Interlocked.CompareExchange(ref _watermarkTicks, next, watermark) == watermark)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// 懒计算模型无后台资源需要释放，保留 Dispose 仅为兼容现有 IDisposable 用法
        /// </summary>
        public void Dispose()
        {
        }
    }
}