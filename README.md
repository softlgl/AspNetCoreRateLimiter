# AspNetCoreRateLimiter
基于 ASP.NET Core 的限流组件，提供令牌桶与漏桶两种算法，支持按路径前缀对请求进行限流。

## 功能特性
- 支持**令牌桶 / 漏桶**两种限流算法
- 通过中间件对请求按**路径前缀**限流，命中即限流，不命中直接放行
- 核心算法基于"时间戳懒计算"实现：**无后台线程、无锁、无队列**，性能高
- 支持三种注册方式：单条规则、规则列表、配置文件

## 参数说明
`RateLimiterOptions` 各字段含义：

| 字段 | 说明 |
| --- | --- |
| `Path` | 限流的路径前缀，如 `/test`（匹配规则见下文） |
| `LimiterType` | 限流算法：`1 = 令牌桶 TokenBucket`，`2 = 漏桶 LeakageBucket` |
| `MaxQPS` | 每秒放行速率（capacity），值越大放行越快 |
| `LimitSize` | 桶容量，即最多允许的**突发请求数** |

> 若 `MaxQPS <= 0` 会被钳制为 `1`；若 `LimitSize <= 0` 会被钳制为 `50`。

## 限流行为
- **非阻塞拒绝模式**：超限时不排队、而是立即拒绝该请求（默认返回 `503`，可自定义）。
- 两种算法的差异体现在**是否允许突发**：
  - **令牌桶（允许突发）**：启动后立即允许 `LimitSize` 次突发请求，空闲会重新攒满突发头寸，之后按 `MaxQPS` 持续放行。
    - 示例：`MaxQPS=1, LimitSize=1` 表示启动后第一个请求放行，下一秒内继续放行 1 个，其余被拒。
  - **漏桶（严格匀速）**：不保留突发头寸，请求按恒定节拍均速放行——两次放行严格间隔 `1/MaxQPS` 秒，空闲不攒突发、同一瞬间只放行一个，过载时无尖峰。
- 二者均采用"时间戳懒计算"：**无后台线程、无锁、无队列**，性能高。

## 路径匹配规则
中间件使用 `StartsWithSegments` 做**前缀段匹配**（大小写不敏感）：

| 配置路径 | 命中的请求 | 不命中的请求 |
| --- | --- | --- |
| `/test` | `/test`、`/test/limiter`、`/Test/x` | `/testxxx`、`/contest` |

- 命中**首个**匹配路径后即停止匹配，使用该路径对应的限流器。
- 所有路径都未命中时直接放行。

## 使用方式
### Asp.Net Core（注册限流规则）
代码注册单个规则：
```cs
services.AddRateLimiter(new RateLimiterOptions
{
    LimiterType = LimiterType.TokenBucket,
    LimitSize = 1,
    MaxQPS = 1,
    Path = "/test"
});
```

代码注册多个规则：
```cs
services.AddRateLimiter(new List<RateLimiterOptions>
{
    new RateLimiterOptions { LimiterType = LimiterType.TokenBucket, LimitSize = 1, MaxQPS = 1, Path = "/test" },
    new RateLimiterOptions { LimiterType = LimiterType.LeakageBucket, LimitSize = 5, MaxQPS = 3, Path = "/api" }
});
```

配置文件注册：
```json
{
  "RateLimiterOptions": [
    {
      "LimiterType": 2,
      "LimitSize": 1,
      "MaxQPS": 1,
      "Path": "/test"
    }
  ]
}
```
```cs
services.AddRateLimiter(_configuration.GetSection("RateLimiterOptions"));
```

### 添加中间件与被限流时的响应
被限流时默认返回 `503 Service Unavailable`：
```cs
app.UseRateLimiter();
```

也可自定义回调（如返回 JSON / 其他状态码）：
```cs
app.UseRouting();
app.UseRateLimiter(async context =>
{
    context.Response.StatusCode = 503;
    await context.Response.WriteAsync("Service Are Limit!!!");
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/test/limiter", async context =>
        await context.Response.WriteAsync("Are You Ok!"));
});
```

### .NET Core Console（非 Web 场景）
```cs
using (var limit = RateLimiter.Create(LimiterType.TokenBucket, 3, 5))
{
    if (limit.Acquire())
    {
        Console.WriteLine("获取成功");
    }
    else
    {
        Console.WriteLine("获取失败");
    }
}
```