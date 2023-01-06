using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AspNetCoreRateLimiter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RateLimiterCore;

namespace WebTest
{
    public class Startup
    {
        private IConfiguration _configuration;
        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddRateLimiter(new List<RateLimiterOptions>{
            //    new RateLimiterOptions{
            //        LimiterType = LimiterType.LeakageBucket,
            //        LimitSize=1,
            //        MaxQPS=1,
            //        Path="/test"
            //    }
            //});

            services.AddRateLimiter(_configuration.GetSection("RateLimiterOptions"));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseRateLimiter(async context=> {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("Service Are Limit!!!");
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/test/limiter", async context =>
                {
                    await context.Response.WriteAsync("Are You Ok!");
                });

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            });
        }
    }
}
