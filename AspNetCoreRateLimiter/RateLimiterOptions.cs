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
        /// 桶大小
        /// </summary>
        public int LimitSize { get; set; }
    }
}
