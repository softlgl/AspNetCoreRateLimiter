using System;
using RateLimiterCore.LimiterService;

namespace RateLimiterCore
{
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
            //https://www.cnblogs.com/yxlblogs/p/10435712.html
            switch (limiterType)
            {
                case LimiterType.TokenBucket:
                default:
                    return new TokenBucketLimiterService(maxQPS, limitSize);
                case LimiterType.LeakageBucket:
                    return new LeakageBucketLimiterService(maxQPS, limitSize);
            }
        }
    }
}
