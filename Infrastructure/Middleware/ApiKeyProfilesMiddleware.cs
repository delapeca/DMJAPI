using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using XNDmjApi.Infrastructure.Options;

namespace XNDmjApi.Infrastructure.Middleware
{
    /// <summary>
    /// Enforça X-Api-Key i resol perfil (PROD/TEST) NOMÉS per rutes:
    ///   /api/SapChangeRequestsUdo/...
    /// La resta de la DMJAPI queda intacta (per ara).
    /// </summary>
    public sealed class ApiKeyProfilesMiddleware
    {
        public const string HeaderName = "X-Api-Key";
        public const string ItemKeyProfile = "DMJ_APIKEY_PROFILE";

        private readonly RequestDelegate _next;

        public ApiKeyProfilesMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IOptions<ApiKeyProfilesOptions> opt)
        {
            if (!context.Request.Path.StartsWithSegments("/api/SapChangeRequestsUdo", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue(HeaderName, out var keyVals))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { ok = false, error = "MISSING_API_KEY" });
                return;
            }

            var apiKey = (keyVals.Count > 0 ? keyVals[0] : "")?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { ok = false, error = "MISSING_API_KEY" });
                return;
            }

            var cfg = opt?.Value ?? new ApiKeyProfilesOptions();

            var prodKey = (cfg.Prod?.ApiKey ?? "").Trim();
            var testKey = (cfg.Test?.ApiKey ?? "").Trim();

            if (string.IsNullOrWhiteSpace(prodKey) && string.IsNullOrWhiteSpace(testKey))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { ok = false, error = "APIKEY_PROFILES_NOT_CONFIGURED" });
                return;
            }

            ResolvedApiKeyProfile? profile = null;

            if (!string.IsNullOrWhiteSpace(prodKey) && string.Equals(apiKey, prodKey, StringComparison.Ordinal))
                profile = new ResolvedApiKeyProfile("prod", cfg.Prod);

            else if (!string.IsNullOrWhiteSpace(testKey) && string.Equals(apiKey, testKey, StringComparison.Ordinal))
                profile = new ResolvedApiKeyProfile("test", cfg.Test);

            if (profile == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { ok = false, error = "INVALID_API_KEY" });
                return;
            }

            // Guardem perfil per consum intern (controller/servei)
            context.Items[ItemKeyProfile] = profile;

            await _next(context);
        }
    }
}
