using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RateLimiterCore;

namespace AspNetCoreRateLimiter
{
    public static class RateLimiterServiceCollectionExtensions
    {
        public static IServiceCollection AddRateLimiter(this IServiceCollection services, RateLimiterOptions limiterOption)
        {
            LimiterCollection limiterCollection = new LimiterCollection();
            limiterCollection.Add(limiterOption.Path,RateLimiter.Create(limiterOption.LimiterType, limiterOption.MaxQPS, limiterOption.LimitSize));
            services.AddSingleton(limiterCollection);
            return services;
        }

        public static IServiceCollection AddRateLimiter(this IServiceCollection services, List<RateLimiterOptions> limiterOptions)
        {
            LimiterCollection limiterCollection = new LimiterCollection();
            foreach (var limiterOption in limiterOptions)
            {
                limiterCollection.Add(limiterOption.Path, RateLimiter.Create(limiterOption.LimiterType, limiterOption.MaxQPS, limiterOption.LimitSize));
            }
            services.AddSingleton(limiterCollection);
            return services;
        }

        public static IServiceCollection AddRateLimiter(this IServiceCollection services, IConfiguration configuration)
        {
            RateLimiterOptions[] rateLimiterOptions = configuration.Get<RateLimiterOptions[]>();
            if (rateLimiterOptions == null || rateLimiterOptions.Length == 0)
            {
                rateLimiterOptions = configuration.GetSection("RateLimiterOptions").Get<RateLimiterOptions[]>();
            }

            if (rateLimiterOptions == null || rateLimiterOptions.Length == 0)
            {
                throw new ArgumentNullException("请在配置文件配置RateLimiterOptions参数");
            }


            return services.AddRateLimiter(rateLimiterOptions.ToList());
        }
    }
}
