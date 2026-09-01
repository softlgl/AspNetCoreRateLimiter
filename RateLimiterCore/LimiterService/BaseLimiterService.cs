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
    /// 当水位超出当前时间一个突发窗口时拒绝请求。
    ///
    /// 子类只需通过构造函数传入 allowBurst 选择自身的过载表现：
    ///  · true（令牌桶）：突发窗口 = (容量-1) 个周期，空闲可重新攒满突发（但不超过容量）；
    ///  · false（漏桶）：突发窗口 = 0，水位每次归到当前时间再推后一个周期，严格匀速、空闲不攒突发。
    /// 两者在构造期一次性固化为只读字段，Acquire 热路径上无虚调用、无重复计算。
    ///
    /// 水位始终不落后于当前时间（推进时取 max(水位, now)），这是"桶容量"能真正封顶的关键：
    /// 否则闲置期间 now 一路前进所拉开的差距，会被一次性兑现成无上限的突发额度。
    ///
    /// 反向的异常也有防护：时钟回跳（VM 在线迁移、容器暂停恢复等会让 now 后退）会让水位
    /// 凭空领先，且要等 now 重新追上才能放行，期间请求全拒。领先量一旦超出合法调用所能
    /// 产生的上界，即判定为回跳并用 CAS 把水位拉回当前时间自愈（每次请求仅多一次比较）。
    /// 读取顺序固定为"先水位、后时间"，因此并发下算出的领先量只会偏小，不会误判。
    /// </summary>
    public abstract class BaseLimiterService : ILimiterService
    {
        /// <summary>
        /// 突发窗口的时间上限（秒）。桶容量换算成窗口时长后不得超过它，
        /// 否则一次突发就能放行远超预期的流量，限流等同失效。
        /// </summary>
        private const int MaxBurstSeconds = 3600;

        protected readonly int _maxQPS;
        protected readonly int _limitSize;
        // 每个令牌周期对应的 Stopwatch ticks 数（两次放行的最小间隔）
        private readonly long _periodTicks;
        // 突发窗口（ticks）：容忍水位超出当前时间的上限。令牌桶 = (容量-1) 个周期，漏桶 = 0
        private readonly long _burstTicks;
        // 时钟回跳判定阈值：任何合法调用下水位的领先量都不可能超过它
        private readonly long _maxLeadTicks;
        // 是否允许突发（令牌桶）：决定额度能否累积，进而决定批量扣减是否要额外预留
        private readonly bool _allowBurst;
        // 水位时间戳，CAS 更新
        private long _watermarkTicks;

        /// <param name="maxQPS">速率（每秒放行数），非正数按 1 处理</param>
        /// <param name="limitSize">桶容量，非正数按 50 处理</param>
        /// <param name="allowBurst">是否允许突发：true = 令牌桶语义，false = 漏桶严格匀速</param>
        protected BaseLimiterService(int maxQPS, int limitSize, bool allowBurst)
        {
            long frequency = Stopwatch.Frequency;

            // 超过计时器频率后周期会被钳死为 1 tick，实际速率无法再提升：显式报错而非静默降级
            if (maxQPS > frequency)
            {
                throw new ArgumentOutOfRangeException(nameof(maxQPS), maxQPS,
                    $"maxQPS 不能超过计时器频率 {frequency}（Stopwatch.Frequency）。");
            }

            // 非正数视为"未配置"，沿用默认值兜底
            _maxQPS = maxQPS > 0 ? maxQPS : 1;
            // 向上取整：宁可略慢于配置速率，也不让实际速率超出
            _periodTicks = Math.Max(1L, (long)Math.Ceiling((double)frequency / _maxQPS));

            // 桶容量决定突发窗口 = (容量-1)/maxQPS 秒，据此反推容量上限
            long maxLimitSize = Math.Min(int.MaxValue, (long)MaxBurstSeconds * _maxQPS);
            if (limitSize > maxLimitSize)
            {
                throw new ArgumentOutOfRangeException(nameof(limitSize), limitSize,
                    $"limitSize 不能超过 {maxLimitSize}，否则突发窗口会超过 {MaxBurstSeconds} 秒，限流将形同虚设。");
            }

            _limitSize = limitSize > 0 ? limitSize : 50;
            _burstTicks = allowBurst ? (_limitSize - 1) * _periodTicks : 0L;
            _allowBurst = allowBurst;
            // 放行瞬间水位相对 now 的领先量上界：
            //   令牌桶 = 突发窗口 + 1 个周期；漏桶 = 本次步进（步进 ≤ 容量个周期）
            // 取两者上界之和，任何合法调用都不会越界，越界即说明时钟发生回跳
            _maxLeadTicks = _burstTicks + _periodTicks * _limitSize;
            // 初始水位设为当前时间：令牌桶由此获得 limitSize 次初始突发
            _watermarkTicks = Stopwatch.GetTimestamp();
        }

        /// <summary>实际生效的速率（每秒放行数）。</summary>
        public int MaxQPS => _maxQPS;

        /// <summary>实际生效的桶容量。注意漏桶为严格匀速，该值不影响其行为。</summary>
        public int LimitSize => _limitSize;

        public bool Acquire(int permits = 1)
        {
            if (permits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(permits), permits, "permits 必须为正数。");
            }
            if (permits > _limitSize)
            {
                throw new ArgumentOutOfRangeException(nameof(permits), permits,
                    $"permits 不能超过桶容量 {_limitSize}——一次索取整个桶以上的额度是没有意义的。");
            }

            long stepTicks = _periodTicks * permits;

            // permits > 1 时需要额外预留的额度：留不出就拒绝，避免"只有 1 个额度却放行了 5 个"。
            // 漏桶不需要预留——它的水位每次都归到当前时间、不累积额度，本身就不存在超发。
            long thresholdTicks = _allowBurst
                ? _burstTicks - (stepTicks - _periodTicks)
                : _burstTicks;

            while (true)
            {
                // 无需 Volatile.Read：后续 CAS 自带内存屏障，读到陈旧值只会导致重试
                long watermark = _watermarkTicks;
                // 先读水位再读时间：并发下读到的是"偏旧的水位 + 偏新的时间"，
                // 领先量只会偏小，不会把正常状态误判成时钟回跳
                long now = Stopwatch.GetTimestamp();

                // 时钟回跳自愈：VM 在线迁移、容器暂停恢复等会让 now 后退，
                // 使水位凭空"领先"一大截，且要等 now 重新追上才能放行——期间所有请求都会被拒。
                // 领先量超出合法调用所能产生的上界，即可判定为回跳，把水位拉回当前时间。
                if (watermark - now > _maxLeadTicks)
                {
                    if (Interlocked.CompareExchange(ref _watermarkTicks, now, watermark) != watermark)
                    {
                        continue; // 被其它线程抢先，重新读取再判定
                    }
                    watermark = now;
                }

                // 关键：水位不得落后于当前时间。
                // 闲置期间水位冻结、now 持续前进，两者差距会无限拉大；若不在此封顶，
                // 这段差距会被一次性兑现成突发额度（闲置越久、放行越多），限流形同虚设。
                long effective = Math.Max(watermark, now);

                // 欠账超过剩余可用额度，拒绝
                if (effective - now > thresholdTicks)
                {
                    return false;
                }

                // 放行：推进水位；并发竞争失败则重试（重试时重新判定，过期请求会被拒）
                long next = effective + stepTicks;
                if (Interlocked.CompareExchange(ref _watermarkTicks, next, watermark) == watermark)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// 距离下一次可放行还需等待的时间。水位相对当前时间每回落一个周期即恢复一个额度，
        /// 因此最早可放行时刻 = 水位 - 突发窗口。
        /// </summary>
        public TimeSpan TimeUntilNextSlot()
        {
            // 这里没有 CAS 兜底，必须真正读到最新水位
            long earliestTicks = Volatile.Read(ref _watermarkTicks) - _burstTicks;
            long waitTicks = earliestTicks - Stopwatch.GetTimestamp();

            if (waitTicks <= 0)
            {
                return TimeSpan.Zero;
            }

            // 时钟回跳期间该值会异常膨胀：钳到水位领先量的合法上界，
            // 避免给客户端回一个荒谬的重试间隔（本方法只读，不自愈——自愈由 Acquire 完成）
            if (waitTicks > _maxLeadTicks)
            {
                waitTicks = _maxLeadTicks;
            }

            return TimeSpan.FromSeconds((double)waitTicks / Stopwatch.Frequency);
        }
    }
}
