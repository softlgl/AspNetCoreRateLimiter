using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        // 构造期固化并按"路径段数降序"排列的限流路径，保证更具体的路径优先命中
        private readonly string[] _paths;

        public RateLimiterMiddleware(RequestDelegate next, LimiterCollection limiterCollection, RequestDelegate callBack)
        {
            _next = next;
            _limiterCollection = limiterCollection;
            _callBack = callBack;
            _paths = limiterCollection.AllPath
                .OrderByDescending(CountSegments)
                .ThenByDescending(p => p.Length)
                .ToArray();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            foreach (string path in _paths)
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
                        // 在回执写入前告知客户端多久后可重试，便于其自行退避
                        SetRetryAfter(context.Response, limiterService.TimeUntilNextSlot());
                        await _callBack(context);
                    }
                    return;
                }
            }

            await _next(context);
        }

        /// <summary>
        /// 按 HTTP 语义写入 Retry-After（整数秒，向上取整且至少 1 秒）。
        /// </summary>
        private static void SetRetryAfter(HttpResponse response, TimeSpan wait)
        {
            int seconds = (int)Math.Ceiling(wait.TotalSeconds);
            if (seconds < 1)
            {
                seconds = 1;
            }

            response.Headers["Retry-After"] = seconds.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 统计路径段数，用于让 /api/private 优先于 /api 命中（最长前缀优先）。
        /// </summary>
        private static int CountSegments(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return 0;
            }

            string trimmed = path.Trim('/');
            if (trimmed.Length == 0)
            {
                return 0;
            }

            int segments = 1;
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] == '/')
                {
                    segments++;
                }
            }
            return segments;
        }
    }
}
