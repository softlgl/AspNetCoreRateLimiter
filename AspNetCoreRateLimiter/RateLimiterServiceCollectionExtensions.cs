using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RateLimiterCore;

namespace AspNetCoreRateLimiter
{
    public static class RateLimiterServiceCollectionExtensions
    {
        public static IServiceCollection AddRateLimiter(this IServiceCollection services,RateLimiterOptions limiterOption)
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
            List<RateLimiterOptions> limiterOptions = configuration.GetValue<List<RateLimiterOptions>>("ratelimiter");
            return services.AddRateLimiter(limiterOptions);
        }
    }
}
