using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RateLimiterCore.LimiterService
{
    public class LeakageBucketLimiterService : BaseLimiterService,ILimiterService
    {
        public LeakageBucketLimiterService(int maxQPS, int limitSize)
            :base(maxQPS, limitSize)
        {
        }

        public bool Acquire()
        {
            if (_bucket.Count >=_limitSize)
            {
                return false;
            }
            lock (_lock)
            {
                if (_bucket.Count >= _limitSize)
                {
                    return false;
                }
                _bucket.Enqueue(new byte());
                return true;
            }
        }

        protected override async Task ProduceAsync()
        {
            int sleep = 1000 / _maxQPS;
            if (sleep == 0)
            {
                sleep = 1;
            }

            while (!_tokenSource.Token.IsCancellationRequested)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                if (_bucket.Count <= 0)
                {
                    continue;
                }
                lock (_lock)
                {
                    if (_bucket.Count > 0)
                    {
                        _bucket.TryDequeue(out var _);
                    }
                }
                stopwatch.Stop();
                int totalTime = Convert.ToInt32(stopwatch.ElapsedMilliseconds);
                if (totalTime < sleep)
                {
                    await Task.Delay(sleep - totalTime);
                }
                else
                {
                    for (int i = 0; i < totalTime / sleep; i++)
                    {
                        lock (_lock)
                        {
                            if (_bucket.Count > 0)
                            {
                                continue;
                            }
                            _bucket.TryDequeue(out var _);
                        }
                    }
                }
            }
        }
    }
}
