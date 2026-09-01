using System;

namespace RateLimiterCore.LimiterService
{
    /// <summary>
    /// 限流服务：基于懒计算时间戳模型，无后台线程、无锁、无队列。
    /// 实现类应当是线程安全的，且通常按路径注册为单例共享。
    /// </summary>
    public interface ILimiterService
    {
        /// <summary>实际生效的速率（每秒放行数）。</summary>
        int MaxQPS { get; }

        /// <summary>实际生效的桶容量。部分算法（如漏桶）不使用该值，仅作配置回读。</summary>
        int LimitSize { get; }

        /// <summary>
        /// 尝试获取 permits 个令牌，立即返回结果（不阻塞等待）。
        /// </summary>
        /// <param name="permits">本次请求消耗的令牌数，必须为正数</param>
        /// <returns>true 表示放行；false 表示被限流</returns>
        bool Acquire(int permits = 1);

        /// <summary>
        /// 距离下一次可以放行还需等待多久；当前立即可放行时返回 <see cref="TimeSpan.Zero"/>。
        /// 可用于给客户端回执（如 HTTP 的 Retry-After）。
        /// 注意：这是基于当前时刻的估算值，并发下可能立即失效。
        /// </summary>
        TimeSpan TimeUntilNextSlot();
    }
}
