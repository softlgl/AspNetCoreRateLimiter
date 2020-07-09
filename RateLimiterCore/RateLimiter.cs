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
        /// <param name="limiterType">类型</param>
        /// <param name="maxQPS">速率</param>
        /// <param name="limitSize">桶容量</param>
        /// <returns></returns>
        public static ILimiterService Create(LimiterType limiterType, int maxQPS, int limitSize)
        {
            return (limiterType) switch
            {
                LimiterType.TokenBucket => new TokenBucketLimiterService(maxQPS, limitSize),
                LimiterType.LeakageBucket => new LeakageBucketLimiterService(maxQPS, limitSize),
                _ => new TokenBucketLimiterService(maxQPS, limitSize)
            };
        }
    }
}
