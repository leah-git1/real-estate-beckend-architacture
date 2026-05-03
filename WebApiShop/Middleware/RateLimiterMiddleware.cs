using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebApiShop.Middleware
{
    public class RateLimiterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimiterMiddleware> _logger;

        private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _clients = new();

        private const int MaxRequests = 10;
        private static readonly TimeSpan WindowSize = TimeSpan.FromSeconds(60);

        public RateLimiterMiddleware(RequestDelegate next, ILogger<RateLimiterMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            string ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            DateTime now = DateTime.UtcNow;

            _clients.AddOrUpdate(
                ip,
                _ => (1, now),
                (_, existing) =>
                {
                    if (now - existing.WindowStart >= WindowSize)
                        return (1, now);

                    return (existing.Count + 1, existing.WindowStart);
                }
            );

            var (count, windowStart) = _clients[ip];

            if (count > MaxRequests)
            {
                DateTime windowEnd = windowStart + WindowSize;
                int retryAfterSeconds = (int)(windowEnd - now).TotalSeconds;

                _logger.LogWarning("Rate limit exceeded for IP {IP}. Count: {Count}/{Max}", ip, count, MaxRequests);

                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                httpContext.Response.ContentType = "application/json";
                httpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();

                var response = new
                {
                    StatusCode = 429,
                    Message = "Too many requests. Please try again later.",
                    RetryAfterSeconds = retryAfterSeconds
                };

                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response));
                return;
            }

            await _next(httpContext);
        }
    }

    public static class RateLimiterExtensions
    {
        public static IApplicationBuilder UseFixedWindowRateLimiter(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RateLimiterMiddleware>();
        }
    }
}
