using System;
using RateLimiterCore;

namespace AspNetCoreRateLimiter
{
    public class RateLimiterOptions
    {
        /// <summary>
        /// 限流路径
        /// </summary>
        public string Path { get; set; }
        
        /// <summary>
        /// 限流算法
        /// </summary>
        public LimiterType LimiterType { get; set; }

        /// <summary>
        /// 每秒速率
        /// </summary>
        public int MaxQPS { get; set; }

        /// <summary>
        /// 桶大小。仅对 <see cref="LimiterType.TokenBucket"/> 生效（决定突发窗口）；
        /// <see cref="LimiterType.LeakageBucket"/> 为严格匀速，突发窗口恒为 0，该值不影响其行为。
        /// </summary>
        public int LimitSize { get; set; }
    }
}
