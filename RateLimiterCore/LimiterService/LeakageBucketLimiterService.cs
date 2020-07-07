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
                _resetEvent.Set();
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

            Stopwatch stopwatch = new Stopwatch();
            while (!_tokenSource.Token.IsCancellationRequested)
            {
                stopwatch.Start();
                if (_bucket.Count > 0)
                {
                    lock (_lock)
                    {
                        if (_bucket.Count > 0)
                        {
                            _bucket.TryDequeue(out var _);
                        }
                        _resetEvent.WaitOne();
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
                                return;
                            }
                            _bucket.TryDequeue(out var _);
                        }
                    }
                }
                stopwatch.Reset();
            }
        }
    }
}
