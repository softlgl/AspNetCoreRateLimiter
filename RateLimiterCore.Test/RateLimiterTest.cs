using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RateLimiterCore.Test
{
    public class RateLimiterTest
    {
        [Fact]
        public void TokenBucketTest()
        {
            using (var limit = RateLimiter.Create(LimiterType.TokenBucket, 3, 5))
            {
                for (int i = 0; i < 50; i++)
                {
                    if (limit.Acquire())
                    {
                        Console.WriteLine($"TokenBucketTest获取成功:{i}");
                    }
                    else
                    {
                        Console.WriteLine($"TokenBucketTest获取失败:{i}");
                    }
                }
            }
        }

        [Fact]
        public void LeakageBucketTest()
        {
            using (var limit = RateLimiter.Create(LimiterType.LeakageBucket, 3, 5))
            {
                for (int i = 0; i < 50; i++)
                {
                    if (limit.Acquire())
                    {
                        Console.WriteLine($"LeakageBucketTest获取成功:{i}");
                    }
                    else
                    {
                        Console.WriteLine($"LeakageBucketTest获取失败:{i}");
                    }
                }
            }
        }
    }
}
