using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace XNDmjApi.Infrastructure.Middleware
{
    public sealed class ApiExceptionJsonMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionJsonMiddleware> _log;
        private readonly IHostEnvironment _env;

        public ApiExceptionJsonMiddleware(RequestDelegate next, ILogger<ApiExceptionJsonMiddleware> log, IHostEnvironment env)
        {
            _next = next;
            _log = log;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            try
            {
                await _next(ctx);
            }
            catch (Exception ex)
            {
                // IMPORTANT: només interceptem errors del nou controller (no toquem la resta d’endpoints)
                if (!ctx.Request.Path.StartsWithSegments("/api/SapChangeRequestsUdo", StringComparison.OrdinalIgnoreCase))
                    throw;

                var traceId = ctx.TraceIdentifier;

                _log.LogError(ex, "Unhandled exception SapChangeRequestsUdo. TraceId={TraceId} Path={Path}", traceId, ctx.Request.Path.Value);

                if (ctx.Response.HasStarted)
                    throw;

                ctx.Response.Clear();
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                ctx.Response.ContentType = "application/json; charset=utf-8";

                bool debug =
                    string.Equals(ctx.Request.Headers["X-DMJ-Debug"], "1", StringComparison.OrdinalIgnoreCase)
                    || _env.IsDevelopment();

                object payload = debug
                    ? new { ok = false, error = "UNHANDLED_EXCEPTION", message = ex.Message, detail = ex.ToString(), traceId }
                    : new { ok = false, error = "INTERNAL_ERROR", traceId };

                await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        }
    }
}
