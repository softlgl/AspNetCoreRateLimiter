using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AspNetCoreRateLimiter;
using Microsoft.AspNetCore.Http;
using RateLimiterCore.LimiterService;
using Xunit;

namespace AspNetCoreRateLimiter.Test
{
    /// <summary>测试用限流服务桩，用于控制 Acquire 的返回值与建议重试间隔。</summary>
    internal class StubLimiterService : ILimiterService
    {
        private readonly bool _result;
        private readonly TimeSpan _wait;
        public int AcquireCalls { get; private set; }
        public int AcquiredPermits { get; private set; }
        public StubLimiterService(bool result, int waitSeconds = 0)
        {
            _result = result;
            _wait = TimeSpan.FromSeconds(waitSeconds);
        }
        public int MaxQPS => 1;
        public int LimitSize => 1;
        public bool Acquire(int permits = 1)
        {
            AcquireCalls++;
            AcquiredPermits += permits;
            return _result;
        }
        public TimeSpan TimeUntilNextSlot() => _wait;
    }

    public class RateLimiterMiddlewareTest
    {
        private static LimiterCollection BuildCollection(string path, ILimiterService service)
        {
            var collection = new LimiterCollection();
            collection.Add(path, service);
            return collection;
        }

        [Fact]
        public async Task 命中限流路径_且未超限_应调用处理器()
        {
            var service = new StubLimiterService(true);
            bool nextCalled = false, callBackCalled = false;

            var middleware = new RateLimiterMiddleware(
                _ => { nextCalled = true; return Task.CompletedTask; },
                BuildCollection("/api", service),
                _ => { callBackCalled = true; return Task.CompletedTask; });

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";

            await middleware.InvokeAsync(context);

            Assert.True(nextCalled);
            Assert.False(callBackCalled);
            Assert.Equal(1, service.AcquireCalls);
        }

        [Fact]
        public async Task 命中限流路径_且被限流_应调用回调()
        {
            var service = new StubLimiterService(false);
            bool nextCalled = false, callBackCalled = false;

            var middleware = new RateLimiterMiddleware(
                _ => { nextCalled = true; return Task.CompletedTask; },
                BuildCollection("/api", service),
                _ => { callBackCalled = true; return Task.CompletedTask; });

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";

            await middleware.InvokeAsync(context);

            Assert.True(callBackCalled);
            Assert.False(nextCalled);
        }

        [Fact]
        public async Task 未命中限流路径_直接放行_不触发回调()
        {
            var service = new StubLimiterService(false);
            bool nextCalled = false, callBackCalled = false;

            var middleware = new RateLimiterMiddleware(
                _ => { nextCalled = true; return Task.CompletedTask; },
                BuildCollection("/api", service),
                _ => { callBackCalled = true; return Task.CompletedTask; });

            var context = new DefaultHttpContext();
            context.Request.Path = "/other/path";

            await middleware.InvokeAsync(context);

            Assert.True(nextCalled);
            Assert.False(callBackCalled);
            Assert.Equal(0, service.AcquireCalls);
        }

        [Fact]
        public async Task 前缀匹配_按路径段匹配_而非任意包含()
        {
            var service = new StubLimiterService(true);

            var middleware = new RateLimiterMiddleware(
                _ => Task.CompletedTask,
                BuildCollection("/api", service),
                _ => Task.CompletedTask);

            // "/apix" 不应命中 "/api"
            var context = new DefaultHttpContext();
            context.Request.Path = "/apix";
            await middleware.InvokeAsync(context);
            Assert.Equal(0, service.AcquireCalls);
        }

        [Fact]
        public async Task 前缀匹配_大小写不敏感()
        {
            var service = new StubLimiterService(true);
            bool nextCalled = false;

            var middleware = new RateLimiterMiddleware(
                _ => { nextCalled = true; return Task.CompletedTask; },
                BuildCollection("/api", service),
                _ => Task.CompletedTask);

            var context = new DefaultHttpContext();
            context.Request.Path = "/Api/V1";
            await middleware.InvokeAsync(context);

            Assert.True(nextCalled);
            Assert.Equal(1, service.AcquireCalls);
        }

        [Fact]
        public async Task 多条路径_优先命中更具体的路径()
        {
            var first = new StubLimiterService(true);
            var second = new StubLimiterService(true);
            var third = new StubLimiterService(true);
            var collection = new LimiterCollection();
            collection.Add("/api", first);
            collection.Add("/api/private", second);
            collection.Add("/admin", third);

            bool callBackCalled = false;
            var middleware = new RateLimiterMiddleware(
                _ => Task.CompletedTask,
                collection,
                _ => { callBackCalled = true; return Task.CompletedTask; });

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/private";
            await middleware.InvokeAsync(context);

            // 最长前缀优先：/api/private 比 /api 更具体，且不依赖注册顺序
            Assert.Equal(0, first.AcquireCalls);
            Assert.Equal(1, second.AcquireCalls);
            Assert.Equal(0, third.AcquireCalls);
            Assert.False(callBackCalled);
        }

        [Fact]
        public async Task 多条路径_命中后只扣减一次_不重复限流()
        {
            var first = new StubLimiterService(true);
            var second = new StubLimiterService(true);
            var third = new StubLimiterService(true);
            var collection = new LimiterCollection();
            collection.Add("/api", first);
            collection.Add("/api/private", second);
            collection.Add("/admin", third);

            var middleware = new RateLimiterMiddleware(
                _ => Task.CompletedTask,
                collection,
                _ => Task.CompletedTask);

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/private/deep";
            await middleware.InvokeAsync(context);

            Assert.Equal(0, first.AcquireCalls);
            Assert.Equal(1, second.AcquireCalls);
            Assert.Equal(0, third.AcquireCalls);
        }

        [Fact]
        public async Task 更具体的路径被限流时_不会回退到较宽松的路径()
        {
            var broad = new StubLimiterService(true);
            var specific = new StubLimiterService(false);
            var collection = new LimiterCollection();
            collection.Add("/api", broad);
            collection.Add("/api/private", specific);

            bool callBackCalled = false;
            var middleware = new RateLimiterMiddleware(
                _ => Task.CompletedTask,
                collection,
                _ => { callBackCalled = true; return Task.CompletedTask; });

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/private";
            await middleware.InvokeAsync(context);

            // 命中 /api/private 后被拒，应走回调而不是转由 /api 重新判定
            Assert.Equal(1, specific.AcquireCalls);
            Assert.Equal(0, broad.AcquireCalls);
            Assert.True(callBackCalled);
        }

        [Fact]
        public async Task 被限流时_响应写入RetryAfter()
        {
            var service = new StubLimiterService(false, waitSeconds: 3);

            var middleware = new RateLimiterMiddleware(
                _ => Task.CompletedTask,
                BuildCollection("/api", service),
                _ => Task.CompletedTask);

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";
            await middleware.InvokeAsync(context);

            Assert.Equal("3", context.Response.Headers["Retry-After"]);
        }

        [Fact]
        public async Task 被限流但可立即重试时_RetryAfter至少为1秒()
        {
            var service = new StubLimiterService(false, waitSeconds: 0);

            var middleware = new RateLimiterMiddleware(
                _ => Task.CompletedTask,
                BuildCollection("/api", service),
                _ => Task.CompletedTask);

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";
            await middleware.InvokeAsync(context);

            // Retry-After 以整数秒为单位，0 秒对客户端没有意义
            Assert.Equal("1", context.Response.Headers["Retry-After"]);
        }

        [Fact]
        public async Task 未被限流时_不写入RetryAfter()
        {
            var service = new StubLimiterService(true, waitSeconds: 5);

            var middleware = new RateLimiterMiddleware(
                _ => Task.CompletedTask,
                BuildCollection("/api", service),
                _ => Task.CompletedTask);

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";
            await middleware.InvokeAsync(context);

            Assert.False(context.Response.Headers.ContainsKey("Retry-After"));
        }
    }
}