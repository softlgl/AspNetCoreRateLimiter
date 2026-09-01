using System;

namespace RateLimiterCore.LimiterService
{
    /// <summary>
    /// 令牌桶限流：允许突发。启动即允许 limitSize 次突发，空闲会重新攒满突发头寸，
    /// 之后按 maxQPS 持续放行。行为完全由基类默认策略（allowBurst = true）决定。
    /// </summary>
    public class TokenBucketLimiterService : BaseLimiterService, ILimiterService
    {
        public TokenBucketLimiterService(int maxQPS, int limitSize)
            : base(maxQPS, limitSize, allowBurst: true)
        {
        }
    }
}
