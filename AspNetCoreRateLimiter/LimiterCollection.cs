using System;
using System.Collections;
using System.Collections.Generic;
using RateLimiterCore.LimiterService;

namespace AspNetCoreRateLimiter
{
    public class LimiterCollection
    {
        private readonly Dictionary<string, ILimiterService> limiters = new Dictionary<string, ILimiterService>();

        public IEnumerable<string> AllPath => limiters.Keys;

        public ILimiterService this[string path]
        {
            get {
                return Get(path);
            }
        }

        public void Add(string path, ILimiterService limiterService) 
        {
            limiters.Add(path,limiterService);
        }

        public ILimiterService Get(string path)
        {
            return limiters[path];
        }
    }
}
