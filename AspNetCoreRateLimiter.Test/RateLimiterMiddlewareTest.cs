using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AspNetCoreRateLimiter;
using Microsoft.AspNetCore.Http;
using RateLimiterCore.LimiterService;
using Xunit;

namespace AspNetCoreRateLimiter.Test
{
    /// <summary>测试用限流服务桩，用于控制 Acquire 的返回值。</summary>
    internal class StubLimiterService : ILimiterService
    {
        private readonly bool _result;
        public int AcquireCalls { get; private set; }
        public StubLimiterService(bool result) => _result = result;
        public bool Acquire()
        {
            AcquireCalls++;
            return _result;
        }
        public void Dispose() { }
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
        public async Task 多条路径_命中首个匹配后停止()
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

            Assert.Equal(1, first.AcquireCalls);
            Assert.Equal(0, second.AcquireCalls);
            Assert.Equal(0, third.AcquireCalls);
            Assert.False(callBackCalled);
        }
    }
}