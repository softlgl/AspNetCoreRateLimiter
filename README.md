# AspNetCoreRateLimiter
AspNetCore限流组件

### 使用方式
#### Asp.Net Core
```cs
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        //注册限流规则
        services.AddRateLimiter(new List<RateLimiterOptions>{
            new RateLimiterOptions{
                LimiterType = LimiterType.TokenBucket,
                LimitSize=1,
                MaxQPS=1,
                Path="/test"
            }
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        //添加中间件
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
```
或使用配置的方式
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "RateLimiterOptions": [
    {
      "LimiterType": 2,
      "LimitSize": 1,
      "MaxQPS": 1,
      "Path": "/test"
    }
  ],
  "AllowedHosts": "*"
}
```
注册时候可以使用配置方式
```csharp
services.AddRateLimiter(_configuration.GetSection("RateLimiterOptions"));
```

#### .Net Core Console
```cs
using (var limit = RateLimiter.Create(LimiterType.TokenBucket, 3, 5))
{
    if (limit.Acquire())
    {
        Console.WriteLine($"TokenBucketTest获取成功:{i}");
    }
    else
    {
        Console.WriteLine($"TokenBucketTest获取失败:{i}");
    }
}
```
