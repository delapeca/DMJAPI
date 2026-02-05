using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace XNDmjApi.Infrastructure.ApiKeys
{
    /// <summary>
    /// IMPORTANT: només aplica a /api/SapChangeRequestsUdo
    /// Resolve X-Api-Key → perfil (CompanyDb + SapUser/Pwd) i el deixa a HttpContext.Items.
    /// </summary>
    public sealed class ApiKeyProfileMiddleware
    {
        public const string HttpContextItemKey = "dmj.api.apikey.profile";

        private readonly RequestDelegate _next;
        private readonly ApiKeyProfilesOptions _opt;

        public ApiKeyProfileMiddleware(RequestDelegate next, IOptions<ApiKeyProfilesOptions> opt)
        {
            _next = next;
            _opt = opt?.Value ?? new ApiKeyProfilesOptions();
        }

        public async Task Invoke(HttpContext ctx)
        {
            if (ctx == null) { await _next(ctx!); return; }

            // Només pel nou controller
            if (!ctx.Request.Path.StartsWithSegments("/api/SapChangeRequestsUdo", StringComparison.OrdinalIgnoreCase))
            {
                await _next(ctx);
                return;
            }

            // Config must exist
            if (_opt.Profiles == null || _opt.Profiles.Count == 0)
            {
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await ctx.Response.WriteAsJsonAsync(new { ok = false, code = "APIKEY_PROFILES_MISSING", message = "Falta config ApiKeyProfiles." });
                return;
            }

            // Header required (ProfileApiKey preferent; fallback temporal X-Api-Key)
            string incoming = "";
            if (ctx.Request.Headers.TryGetValue("ProfileApiKey", out var hvNew))
                incoming = (hvNew.ToString() ?? "").Trim();
            else if (ctx.Request.Headers.TryGetValue("X-Api-Key", out var hvOld))
                incoming = (hvOld.ToString() ?? "").Trim();

            if (string.IsNullOrWhiteSpace(incoming))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { ok = false, code = "MISSING_API_KEY", message = "Falta header ProfileApiKey (o X-Api-Key durant la transició)." });
                return;
            }
if (string.IsNullOrWhiteSpace(incoming))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { ok = false, code = "MISSING_API_KEY", message = "X-Api-Key buit." });
                return;
            }

            var prof = ResolveProfile(incoming);
            if (prof == null)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { ok = false, code = "INVALID_API_KEY", message = "X-Api-Key incorrecta." });
                return;
            }

            ctx.Items[HttpContextItemKey] = prof;
            await _next(ctx);
        }

        private ApiKeyProfile? ResolveProfile(string incoming)
        {
            foreach (var kv in _opt.Profiles)
            {
                string name = kv.Key ?? "";
                var cfg = kv.Value;
                if (cfg == null) continue;

                string cfgKey = (cfg.ApiKey ?? "").Trim();
                if (string.IsNullOrWhiteSpace(cfgKey)) continue;

                if (FixedEquals(incoming, cfgKey))
                {
                    return new ApiKeyProfile(
                        name: name,
                        companyDb: (cfg.CompanyDb ?? "").Trim(),
                        sapUser: (cfg.SapUser ?? "").Trim(),
                        sapPassword: (cfg.SapPassword ?? "").Trim()
                    );
                }
            }
            return null;
        }

        private static bool FixedEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            var ba = Encoding.UTF8.GetBytes(a);
            var bb = Encoding.UTF8.GetBytes(b);
            if (ba.Length != bb.Length) return false;
            return CryptographicOperations.FixedTimeEquals(ba, bb);
        }
    }
}

