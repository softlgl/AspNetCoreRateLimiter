using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreRateLimiter
{
    public class RateLimiterMiddleware
    {

        private readonly RequestDelegate _next;
        public RateLimiterMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            
            await _next(context);
        }
    }
}
