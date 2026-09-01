using System;
using RateLimiterCore.LimiterService;

namespace RateLimiterCore
{
    //https://www.cnblogs.com/yxlblogs/p/10435712.html
    public static class RateLimiter
    {
        /// <summary>
        /// 创建限流服务
        /// </summary>
        /// <param name="limiterType">类型；未知值回退到令牌桶</param>
        /// <param name="maxQPS">速率（每秒放行数），非正数按 1 处理，超过计时器频率将抛异常</param>
        /// <param name="limitSize">
        /// 桶容量，非正数按 50 处理。仅对令牌桶生效（决定突发窗口）；
        /// 漏桶为严格匀速，突发窗口恒为 0，该参数不影响其行为。
        /// </param>
        /// <returns></returns>
        public static ILimiterService Create(LimiterType limiterType, int maxQPS, int limitSize)
        {
            return limiterType switch
            {
                LimiterType.TokenBucket => new TokenBucketLimiterService(maxQPS, limitSize),
                LimiterType.LeakageBucket => new LeakageBucketLimiterService(maxQPS, limitSize),
                _ => new TokenBucketLimiterService(maxQPS, limitSize)
            };
        }
    }
}
