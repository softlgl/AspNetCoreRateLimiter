using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RateLimiterCore.LimiterService
{
    public abstract class BaseLimiterService:IDisposable
    {
        protected readonly int _maxQPS;
        protected readonly int _limitSize;
        protected readonly Queue<byte> _bucket;
        protected readonly CancellationTokenSource _tokenSource;
        protected volatile bool _disposed;
        protected readonly TimeSpan _timeSpan;
        protected readonly object _lock = new object();

        public BaseLimiterService(int maxQPS, int limitSize)
        {
            _maxQPS = maxQPS;
            _limitSize = limitSize;
            _bucket = new Queue<byte>();
            _tokenSource = new CancellationTokenSource();
            if (_limitSize <= 0)
            {
                _limitSize = 50;
            }
            if (_maxQPS <= 0)
            {
                _maxQPS = 1;
            }
            Task.Run(ProduceAsync, _tokenSource.Token);
        }


        protected abstract Task ProduceAsync();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _tokenSource.Cancel();
                _disposed = true;
            }
        }
    }
}
