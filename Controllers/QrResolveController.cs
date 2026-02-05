using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using XNDmjApi.Loxone.Data;
using XNDmjApi.Loxone.Options;
using XNDmjApi.QrMulti.Models;
using XNDmjApi.QrMulti.Dtos;
using System.Diagnostics;


namespace XNDmjApi.Controllers
{
    /// <summary>
    /// Resolve de tokens interns (QR -> token opac) i retorna un manifest JSON.
    /// IMPORTANT:
    ///  - NO executa res.
    ///  - Només descriu què és el token: type = "loxone" | "multi".
    ///  - Protegit amb header X-API-Key (o X-Api-Key).
    /// </summary>
    [ApiController]
    [Route("api/qr")]
    public class QrResolveController : ControllerBase
    {
        private static readonly Regex Token32Hex =
            new Regex("(?i)^[0-9a-f]{32}$", RegexOptions.Compiled);

        private readonly AppDbContext _db;
        private readonly AdminOptions _admin;

        public QrResolveController(AppDbContext db, IOptions<AdminOptions> admin)
        {
            _db = db;
            _admin = admin.Value;
        }

        /// <summary>
        /// GET /api/qr/resolve/{token}
        /// Header requerit: X-API-Key (o X-Api-Key)
        /// </summary>
        [HttpGet("resolve/{token}")]
        public async Task<IActionResult> Resolve([FromRoute] string token, CancellationToken ct)
        {
            // 1) Auth
            if (!TryAuthorizeClientKey(out var authFail))
                return authFail!;

            // 2) Validació format token
            token = (token ?? string.Empty).Trim();
            if (!Token32Hex.IsMatch(token))
                return BadRequest(new { error = "INVALID_TOKEN_FORMAT" });

            Debug.WriteLine($"[QrResolve] DataSource={_db.Database.GetDbConnection().DataSource}");

            // 3) Primer: LOXONE (QrTokens)
            var t = await _db.QrTokens
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Token == token, ct);

            if (t != null)
            {
                var action = await _db.LoxoneActions
                    .AsNoTracking()
                    .SingleOrDefaultAsync(a => a.Code == t.ActionCode, ct);

                return Ok(new
                {
                    type = "loxone",
                    token = t.Token,
                    actionCode = t.ActionCode,
                    actionName = action?.Name,
                    loxoneCommand = action?.LoxoneCommand,

                    createdAt = t.CreatedAt,
                    expiresAt = t.ExpiresAt,
                    maxUses = t.MaxUses,
                    usesCount = t.UsesCount,
                    revoked = t.Revoked
                });
            }


            var m = await _db.QrManifests
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Token == token, ct);

            if (m != null)
            {
                // ✅ Multi: retornem el manifest FUTUR tal qual (sense wrapper ni conversions)
                return Content(m.PayloadJson, "application/json");
            }


            return NotFound(new { error = "TOKEN_NOT_FOUND" });
        }

        private async Task EnsureQrManifestsTableAsync(CancellationToken ct)
        {
            const string sql = @"
            CREATE TABLE IF NOT EXISTS QrManifests (
                Token TEXT PRIMARY KEY NOT NULL,
                PayloadJson TEXT NOT NULL,
                ItemCode TEXT NULL,
                PanelCode TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_QrManifests_ItemCode ON QrManifests(ItemCode);
            CREATE INDEX IF NOT EXISTS IX_QrManifests_PanelCode ON QrManifests(PanelCode);
            ";
            await _db.Database.ExecuteSqlRawAsync(sql, ct);
        }

        private static JsonElement NormalizeMultiOptionsJson(string payloadJson)
        {
            // Converteix options[].kind -> options[].type (compatibilitat)
            // i retorna l'array options com JsonElement.
            var node = JsonNode.Parse(payloadJson) as JsonObject;
            if (node == null) return JsonDocument.Parse("[]").RootElement;

            var options = node["options"] as JsonArray;
            if (options == null) return JsonDocument.Parse("[]").RootElement;

            foreach (var optNode in options)
            {
                if (optNode is JsonObject optObj)
                {
                    if (optObj["type"] == null && optObj["kind"] != null)
                    {
                        optObj["type"] = optObj["kind"]!.GetValue<string>();
                        optObj.Remove("kind");
                    }
                }
            }

            var normalizedJson = options.ToJsonString();
            return JsonDocument.Parse(normalizedJson).RootElement;
        }

        private bool TryAuthorizeClientKey(out IActionResult? fail)
        {
            fail = null;

            var expected = (_admin?.ApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(expected))
            {
                fail = StatusCode(500, new { error = "ADMIN_APIKEY_NOT_CONFIGURED" });
                return false;
            }

            // Accepta X-Api-Key (Swagger) i X-API-Key (AR), mateix valor
            string? provided = null;

            if (Request.Headers.TryGetValue("X-Api-Key", out var apiKeyVal))
                provided = apiKeyVal.ToString().Trim();
            else if (Request.Headers.TryGetValue("X-API-Key", out var showroomVal))
                provided = showroomVal.ToString().Trim();

            if (string.IsNullOrWhiteSpace(provided))
            {
                fail = Unauthorized(new { error = "UNAUTHORIZED" });
                return false;
            }

            if (!string.Equals(provided, expected, StringComparison.Ordinal))
            {
                fail = Unauthorized(new { error = "UNAUTHORIZED" });
                return false;
            }

            return true;
        }
    }
}
