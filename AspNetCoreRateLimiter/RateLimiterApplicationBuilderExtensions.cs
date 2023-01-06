using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreRateLimiter
{
    public static class RateLimiterApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseRateLimiter(this IApplicationBuilder builder)
        {
            return builder.UseRateLimiter(async context=> {
                context.Response.StatusCode=503;
                await context.Response.WriteAsync("Service Unavailable");
            });
        }

        public static IApplicationBuilder UseRateLimiter(this IApplicationBuilder builder, RequestDelegate requestDelegate)
        {
            return builder.UseMiddleware<RateLimiterMiddleware>(requestDelegate);
        }
    }
}
