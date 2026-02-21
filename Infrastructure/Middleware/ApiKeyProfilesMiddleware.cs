using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using XNDmjApi.Infrastructure.Options;

namespace XNDmjApi.Infrastructure.Middleware
{
    /*
     * ==========================================================================================
     * LEGACY (ABANS) — Referència / NO s’executa (només per tenir-ho documentat)
     * ==========================================================================================
     *
     * Abans aquest middleware acostumava a ser “mínim”:
     *   - Només llegia "X-Api-Key"
     *   - Només aplicava a: /api/SapChangeRequestsUdo/...
     *   - NO tenia header nou ProfileApiKey
     *   - NO tenia “path list” extensible
     *
     * (Exemple conceptual — NO copiat literalment per evitar duplicats de codi real):
     *
     *   if (!path.StartsWithSegments("/api/SapChangeRequestsUdo")) { next(); return; }
     *   if (!Headers.TryGetValue("X-Api-Key", out key)) return 401;
     *   if (key == prodKey) profile=prod; else if (key == testKey) profile=test; else 401;
     *   Items["DMJ_APIKEY_PROFILE"]=profile;
     *
     * Canvi fet ara:
     *   - Header preferit: ProfileApiKey
     *   - Fallback: X-Api-Key (+ variants)
     *   - Aplica també a: /api/clients/self/...
     *   - Tot el flux de “selecció PROD/TEST” queda CENTRALITZAT aquí (sense tocar endpoints un per un)
     *
     * IMPORTANT:
     *   Aquest fitxer és XNDmjApi.Infrastructure.Middleware.ApiKeyProfilesMiddleware.
     *   Si el teu Program.cs crida un ALTRE middleware (p.ex. ApiKeyProfileMiddleware a un altre namespace),
     *   aquest NO s’executarà fins que el Program.cs l’utilitzi.
     */

    /// <summary>
    /// ApiKeyProfilesMiddleware
    /// -----------------------
    /// Objectiu:
    ///   1) Llegir una API key d'entrada
    ///   2) Resoldre el "perfil" (PROD/TEST) segons configuració (ApiKeyProfilesOptions)
    ///   3) Guardar el perfil a HttpContext.Items perquè Controllers/Services decideixin BD/credencials segons perfil.
    ///
    /// IMPORTANT (migració pas a pas):
    ///   - Header "nou" (preferit):  ProfileApiKey
    ///   - Header "legacy" (fallback): X-Api-Key (i variants habituals)
    ///   - Això permet que endpoints que JA funcionen amb X-Api-Key no es trenquin,
    ///     mentre vas passant clients a ProfileApiKey progressivament.
    ///
    /// Afegir més endpoints a aquest middleware:
    ///   - Només cal afegir un prefix a PathPrefixes (ex: "/api/Clients/Foo")
    ///   - NO cal tocar la lògica de resolució de perfil.
    /// </summary>
    public sealed class ApiKeyProfilesMiddleware
    {
        // Header principal (nou): és el que vols fer servir a partir d'ara
        public const string PrimaryHeaderName = "ProfileApiKey";

        // Header legacy (fallback): el que ja tenies/teniu a alguns clients
        public const string LegacyHeaderName = "X-Api-Key";

        // Clau on desem el perfil resolt dins HttpContext.Items
        // NOTA: mantén aquesta constant estable, perquè controllers/serveis la consultaran.
        public const string ItemKeyProfile = "DMJ_APIKEY_PROFILE";

        private readonly RequestDelegate _next;

        public ApiKeyProfilesMiddleware(RequestDelegate next) => _next = next;

        // ─────────────────────────────────────────────────────────────────────────────
        // PAS 0 (IMPORTANT): On s'aplica?
        //
        // Aquesta llista és la “clau” del sistema:
        //   - Si un endpoint NO està sota un d’aquests prefixos → el middleware NO intervé → la DMJAPI queda intacta.
        //   - Si un endpoint SÍ està sota un d’aquests prefixos → exigim key + resolució PROD/TEST.
        //
        // Afegir un nou bloc d’endpoints és TAN SIMPLE com afegir un nou prefix aquí.
        // Exemple:
        //   "/api/items", "/api/documents", etc. (quan decideixis migrar-los)
        // ─────────────────────────────────────────────────────────────────────────────
        private static readonly string[] PathPrefixes = new[]
        {
            // Ja existia (ChangeRequests UDO)
            "/api/SapChangeRequestsUdo",

            // Nou: self-service de clients (profile/contacts/addresses/balances...)
            "/api/clients/self",
        };

        public async Task InvokeAsync(HttpContext context, IOptions<ApiKeyProfilesOptions> opt)
        {
            // 1) A quines rutes aplica aquest middleware?
            //    - Si la ruta no coincideix amb cap prefix → passem de llarg i NO toquem res.
            var p = context.Request.Path;
            if (!AppliesToPath(p))
            {
                await _next(context);
                return;
            }

            // 2) Llegeix la key:
            //    - Primer intenta ProfileApiKey
            //    - Si no hi és, intenta X-Api-Key i variants (compatibilitat)
            //    - Si encara no hi és, intenta querystring (fallback útil per Swagger/manuals)
            if (!TryGetIncomingApiKey(context, out var apiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { ok = false, error = "MISSING_API_KEY" });
                return;
            }

            // 3) Carrega configuració de perfils (Prod/Test) des de appsettings.json / secrets / env
            //    Secció esperada: "ApiKeyProfiles"
            var cfg = opt?.Value ?? new ApiKeyProfilesOptions();

            var prodKey = (cfg.Prod?.ApiKey ?? "").Trim();
            var testKey = (cfg.Test?.ApiKey ?? "").Trim();

            if (string.IsNullOrWhiteSpace(prodKey) && string.IsNullOrWhiteSpace(testKey))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { ok = false, error = "APIKEY_PROFILES_NOT_CONFIGURED" });
                return;
            }

            // 4) Resolveix perfil segons key (comparació exacta, case-sensitive)
            //    IMPORTANT: mantenim comparació exacta per evitar sorpreses.
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

            // 5) Guardem el perfil per consum intern (controller/servei)
            //    Aquest és el “punt d’enllaç” perquè:
            //      - el controller agafi el perfil
            //      - i el service decideixi BD/credencials segons profile.Profile.CompanyDb / SapUser / SapPassword ...
            //
            //    Exemple d'ús en un controller:
            //      var prof = HttpContext.Items[ApiKeyProfilesMiddleware.ItemKeyProfile] as ResolvedApiKeyProfile;
            //      if (prof == null) return Unauthorized(...);
            //      var db = prof.Profile.CompanyDb;
            context.Items[ItemKeyProfile] = profile;

            await _next(context);
        }

        /// <summary>
        /// Decideix si el middleware s'ha d'aplicar a la ruta actual.
        /// Afegir més endpoints = afegir més prefixos a PathPrefixes (NO tocar aquesta funció).
        /// </summary>
        private static bool AppliesToPath(PathString path)
        {
            // Recorrem tots els prefixos configurats i comprovem si la request hi cau dins.
            foreach (var pref in PathPrefixes)
            {
                if (path.StartsWithSegments(pref, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Resol la API key entrant amb ordre de preferència:
        ///  1) Header ProfileApiKey (nou)
        ///  2) Header X-Api-Key (legacy) + variants habituals
        ///  3) Query string (fallback)
        ///
        /// NOTA: mantenim els fallback per NO trencar clients existents.
        /// Quan acabis la migració, podràs eliminar els legacy amb seguretat.
        /// </summary>
        private static bool TryGetIncomingApiKey(HttpContext ctx, out string apiKey)
        {
            apiKey = "";

            // 1) Header nou (preferit)
            if (TryGetHeader(ctx, PrimaryHeaderName, out apiKey))
                return true;

            // 2) Headers legacy (compatibilitat)
            if (TryGetHeader(ctx, LegacyHeaderName, out apiKey)) return true;
            if (TryGetHeader(ctx, "X-API-Key", out apiKey)) return true;
            if (TryGetHeader(ctx, "x-api-key", out apiKey)) return true;

            // antics / swagger antics
            if (TryGetHeader(ctx, "api_key", out apiKey)) return true;
            if (TryGetHeader(ctx, "Api-Key", out apiKey)) return true;

            // 3) Querystring fallback (útil per proves/Swagger/manual)
            //    - també admetem ProfileApiKey per coherència
            apiKey = (ctx.Request.Query["ProfileApiKey"].ToString() ?? "").Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(apiKey)) return true;

            apiKey = (ctx.Request.Query["profileApiKey"].ToString() ?? "").Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(apiKey)) return true;

            apiKey = (ctx.Request.Query["api_key"].ToString() ?? "").Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(apiKey)) return true;

            apiKey = (ctx.Request.Query["apiKey"].ToString() ?? "").Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(apiKey)) return true;

            apiKey = (ctx.Request.Query["X-Api-Key"].ToString() ?? "").Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(apiKey)) return true;

            return false;
        }

        /// <summary>
        /// Llegeix un header concret i normalitza:
        ///  - Trim d'espais i cometes
        /// </summary>
        private static bool TryGetHeader(HttpContext ctx, string headerName, out string value)
        {
            value = "";
            if (!ctx.Request.Headers.TryGetValue(headerName, out var vals))
                return false;

            value = (vals.Count > 0 ? vals[0] : "")?.Trim().Trim('"') ?? "";
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
