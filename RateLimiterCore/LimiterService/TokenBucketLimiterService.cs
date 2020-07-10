using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RateLimiterCore.LimiterService
{
    public class TokenBucketLimiterService: BaseLimiterService, ILimiterService 
    {
        public TokenBucketLimiterService(int maxQPS, int limitSize)
            :base(maxQPS, limitSize)
        {
            for (int i = 0; i < limitSize; i++)
            {
                _bucket.Enqueue(new byte());
            }
        }

        public bool Acquire()
        {
            if (_bucket.Count <= 0)
            {
                return false;
            }
            lock (_lock)
            {
                if (_bucket.Count <= 0)
                {
                    return false;
                }
                if (_bucket.TryDequeue(out var _))
                {
                    return true;
                }
                return false;
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
                lock (_lock)
                {
                    if (_bucket.Count >= _limitSize)
                    {
                        continue;
                    }
                    _bucket.Enqueue(new byte());
                }
                stopwatch.Stop();
                int totalTime = Convert.ToInt32(stopwatch.ElapsedMilliseconds);
                if (totalTime < sleep)
                {
                    await Task.Delay(sleep - totalTime);
                }
                else
                {
                    for (int i = 0; i < totalTime/ sleep; i++)
                    {
                        lock (_lock)
                        {
                            if (_bucket.Count >= _limitSize)
                            {
                                continue;
                            }
                            _bucket.Enqueue(new byte());
                        }
                    }
                }
            }
        }
    }
}
