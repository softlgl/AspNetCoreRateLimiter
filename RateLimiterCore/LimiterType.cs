using System;
using System.ComponentModel;

namespace RateLimiterCore
{
    public enum LimiterType:byte
    {
        [Description("令牌桶")]
        TokenBucket = 1,

        [Description("漏桶")]
        LeakageBucket = 2
    }
}
