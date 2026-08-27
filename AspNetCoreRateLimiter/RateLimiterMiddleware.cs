using System;
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
            foreach (string path in _allPath)
            {
                // 按路径段前缀匹配（大小写不敏感）：
                // 配置 /test 命中 /test 与 /test/limiter，但不会命中 /contest 或 /testxxx
                if (context.Request.Path.StartsWithSegments(path))
                {
                    ILimiterService limiterService = _limiterCollection[path];
                    if (limiterService.Acquire())
                    {
                        await _next(context);
                    }
                    else
                    {
                        await _callBack(context);
                    }
                    return;
                }
            }

            await _next(context);
        }
    }
}
