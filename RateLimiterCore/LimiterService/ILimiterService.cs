using System;
namespace RateLimiterCore.LimiterService
{
    public interface ILimiterService:IDisposable
    {
        bool Acquire();
    }
}
