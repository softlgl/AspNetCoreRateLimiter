using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RateLimiterCore.LimiterService;

namespace AspNetCoreRateLimiter
{
    public class RateLimiterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RequestDelegate _callBack;
        private readonly LimiterCollection _limiterCollection;
        private readonly IEnumerable<string> _allPath;

        public RateLimiterMiddleware(RequestDelegate next, LimiterCollection limiterCollection, RequestDelegate callBack)
        {
            _next = next;
            _limiterCollection = limiterCollection;
            _callBack = callBack;
            _allPath = _limiterCollection.AllPath;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string path = _allPath.FirstOrDefault(i => context.Request.Path.Value.Contains(i));
            if (string.IsNullOrEmpty(path))
            {
                await _next(context);
                return;
            }

            ILimiterService limiterService = _limiterCollection[path];
            if (limiterService.Acquire())
            {
                await _next(context);
                return;
            }
            
            await _callBack(context);
        }
    }
}
