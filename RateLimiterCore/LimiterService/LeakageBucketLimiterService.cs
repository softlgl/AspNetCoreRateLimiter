using System;

namespace RateLimiterCore.LimiterService
{
    /// <summary>
    /// 漏桶限流：严格匀速（不保留突发头寸）。
    /// 每次放行把水位钳制到当前时间再推后一个周期，请求按恒定节拍均速放行，
    /// 空闲时不攒突发、请求被拒后也无法"提前透支"下一次放行。
    ///
    /// 注意：严格匀速与"桶容量"在语义上互斥（突发窗口恒为 0），因此 limitSize 对本类
    /// 的行为没有任何影响，仅作为配置值保留在 <see cref="BaseLimiterService.LimitSize"/> 供诊断。
    /// 需要突发能力请改用 <see cref="TokenBucketLimiterService"/>。
    /// </summary>
    public class LeakageBucketLimiterService : BaseLimiterService, ILimiterService
    {
        public LeakageBucketLimiterService(int maxQPS, int limitSize)
            : base(maxQPS, limitSize, allowBurst: false)
        {
        }
    }
}
