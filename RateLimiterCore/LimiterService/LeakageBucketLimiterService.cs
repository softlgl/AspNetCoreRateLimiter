using System;

namespace RateLimiterCore.LimiterService
{
    /// <summary>
    /// 漏桶限流：严格匀速（不保留突发头寸）。
    /// 每次放行把水位钳制到当前时间再推后一个周期，请求按恒定节拍均速放行，
    /// 空闲时不攒突发、请求被拒后也无法"提前透支"下一次放行。
    /// </summary>
    public class LeakageBucketLimiterService : BaseLimiterService, ILimiterService
    {
        public LeakageBucketLimiterService(int maxQPS, int limitSize)
            : base(maxQPS, limitSize)
        {
        }

        // 严格匀速：不允许任何超出当前时间的欠账（突发=0）
        protected override long BurstTicks => 0;

        // 放行后水位 = 当前时间 + 一个周期：两次放行严格间隔一个周期，且闲置不攒突发
        protected override long AdvanceWatermark(long watermark, long now) => now + _periodTicks;
    }
}