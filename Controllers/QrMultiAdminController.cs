using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using XNDmjApi.Loxone.Data;
using XNDmjApi.Loxone.Options;
using XNDmjApi.QrMulti.Dtos;
using XNDmjApi.QrMulti.Models;
using XNDmjApi.QrMulti.Services;
using Swashbuckle.AspNetCore.Filters;


namespace XNDmjApi.Controllers
{
    [ApiController]
    [Route("api/qr/multi")]
    public class QrMultiAdminController : ControllerBase
    {
        private static readonly Regex Token32Hex =
            new Regex("(?i)^[0-9a-f]{32}$", RegexOptions.Compiled);

        private readonly AppDbContext _db;
        private readonly AdminOptions _admin;

        public QrMultiAdminController(AppDbContext db, IOptions<AdminOptions> admin)
        {
            _db = db;
            _admin = admin.Value;
        }

        // POST /api/qr/multi
        // Header: X-Api-Key: ...
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] JsonElement manifest, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;

            // Body obligatori i ha de ser un objecte JSON
            if (manifest.ValueKind != JsonValueKind.Object)
                return BadRequest(new { error = "MISSING_BODY" });

            // Guardem TAL QUAL (equivalent) el JSON rebut
            var payloadJson = manifest.GetRawText();

            // Parse per validar mínim i derivar camps
            var root = JsonNode.Parse(payloadJson) as JsonObject;
            if (root == null)
                return BadRequest(new { error = "INVALID_JSON_OBJECT" });

            var type = root["type"]?.GetValue<string>()?.Trim();
            if (!string.Equals(type, "multi", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "INVALID_TYPE", type });

            var options = root["options"] as JsonArray;
            if (options == null || options.Count == 0)
                return BadRequest(new { error = "MISSING_OPTIONS" });

            // Allowed types del contracte FUTUR
            var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "itemcode", "url", "panel", "card", "wifi", "loxone"
            };

            string? derivedItemCode = null;
            string? derivedPanelCode = null;

            foreach (var optNode in options)
            {
                var opt = optNode as JsonObject;
                if (opt == null)
                    return BadRequest(new { error = "OPTION_INVALID" });

                var optType = opt["type"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(optType))
                    return BadRequest(new { error = "OPTION_TYPE_REQUIRED" });

                // normalitzem per comparar (sense canviar el JSON guardat)
                var optTypeNorm = optType.ToLowerInvariant();
                if (!allowedTypes.Contains(optTypeNorm))
                    return BadRequest(new { error = "OPTION_TYPE_INVALID", type = optType });

                // Derivats recomanats (primer match)
                if (derivedItemCode == null && optTypeNorm == "itemcode")
                    derivedItemCode = opt["code"]?.GetValue<string>()?.Trim();

                if (derivedPanelCode == null && optTypeNorm == "panel")
                    derivedPanelCode = opt["ref"]?.GetValue<string>()?.Trim();
            }

            var token = QrTokenGenerator.NewTokenHex(16);

            var row = new QrManifest
            {
                Token = token,
                PayloadJson = payloadJson,
                ItemCode = string.IsNullOrWhiteSpace(derivedItemCode) ? null : derivedItemCode,
                PanelCode = string.IsNullOrWhiteSpace(derivedPanelCode) ? null : derivedPanelCode,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.QrManifests.Add(row);
            await _db.SaveChangesAsync(ct);

            // Response mínim (tal com vols per la Intranet)
            return Ok(new
            {
                ok = true,
                token = row.Token,
                itemCode = row.ItemCode,
                panelCode = row.PanelCode,
                createdAt = row.CreatedAt.UtcDateTime.ToString("o")
            });
        }


        // GET /api/qr/multi?itemCode=.&panelCode=.&q=.&take=50
        // Header: X-Api-Key: .
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? itemCode,[FromQuery] string? panelCode,[FromQuery] string? q,[FromQuery] int? take,CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;

            var query = _db.QrManifests.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(itemCode))
            {
                var v = itemCode.Trim();
                query = query.Where(x => x.ItemCode != null && x.ItemCode == v);
            }

            if (!string.IsNullOrWhiteSpace(panelCode))
            {
                var v = panelCode.Trim();
                query = query.Where(x => x.PanelCode != null && x.PanelCode == v);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var v = q.Trim();
                // Search simple: token / item / panel
                query = query.Where(x =>
                    x.Token.Contains(v) ||
                    (x.ItemCode != null && x.ItemCode.Contains(v)) ||
                    (x.PanelCode != null && x.PanelCode.Contains(v)));
            }

            var limit = take.HasValue ? Math.Clamp(take.Value, 1, 500) : 100;

            // IMPORTANT: no fem OrderBy(CreatedAt) a SQL per evitar NotSupported amb DateTimeOffset.
            var rows = await query.ToListAsync(ct);

            string? ReadStringField(string payloadJson, string field)
            {
                try
                {
                    var node = JsonNode.Parse(payloadJson) as JsonObject;
                    return node?[field]?.GetValue<string>();
                }
                catch
                {
                    return null;
                }
            }

            var ordered = rows
                .OrderByDescending(x => x.CreatedAt) // LINQ to Objects, OK
                .Take(limit)
                .Select(x => new
                {
                    type = "multi",
                    token = x.Token,
                    label = string.IsNullOrWhiteSpace(x.PayloadJson) ? null : ReadStringField(x.PayloadJson, "label"),
                    code = string.IsNullOrWhiteSpace(x.PayloadJson) ? null : ReadStringField(x.PayloadJson, "code"),
                    createdAt = x.CreatedAt.UtcDateTime.ToString("o")
                })
                .ToList();

            return Ok(new { items = ordered, count = ordered.Count });
        }


        // GET /api/qr/multi/{token}
        // Header: X-Api-Key: .
        [HttpGet("{token}")]
        public async Task<IActionResult> Get([FromRoute] string token, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;

            token = (token ?? string.Empty).Trim();
            if (!Token32Hex.IsMatch(token))
                return BadRequest(new { error = "INVALID_TOKEN_FORMAT" });

            var row = await _db.QrManifests
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Token == token, ct);

            if (row == null)
                return NotFound(new { error = "NOT_FOUND" });

            // ✅ Retornem el manifest guardat TAL QUAL (FUTUR)
            return Content(row.PayloadJson, "application/json");
        }


        // DELETE /api/qr/multi/{token}
        // Header: X-Api-Key: ...
        [HttpDelete("{token}")]
        public async Task<IActionResult> Delete([FromRoute] string token, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;

            token = (token ?? string.Empty).Trim();
            if (!Token32Hex.IsMatch(token))
                return BadRequest(new { error = "INVALID_TOKEN_FORMAT" });

            var row = await _db.QrManifests.SingleOrDefaultAsync(x => x.Token == token, ct);
            if (row == null) return NotFound(new { error = "NOT_FOUND" });

            _db.QrManifests.Remove(row);
            await _db.SaveChangesAsync(ct);

            return Ok(new { ok = true, token });
        }

        private bool IsAdminAuthorized(out IActionResult? fail)
        {
            fail = null;

            var expected = (_admin?.ApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(expected))
            {
                fail = StatusCode(500, new { error = "ADMIN_APIKEY_NOT_CONFIGURED" });
                return false;
            }

            // Accepta X-Api-Key (Swagger) i X-API-Key (clients/AR), mateix valor
            string? provided = null;

            if (Request.Headers.TryGetValue("AdminApiKey", out var got0))
                provided = got0.ToString().Trim();
            else if (Request.Headers.TryGetValue("X-Api-Key", out var got))
                provided = got.ToString().Trim();
            else if (Request.Headers.TryGetValue("X-API-Key", out var got2))
                provided = got2.ToString().Trim();
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

        // PUT /api/qr/multi/{token}
        // Header: X-Api-Key: .
        [HttpPut("{token}")]
        public async Task<IActionResult> Update([FromRoute] string token, [FromBody] JsonElement manifest, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;

            token = (token ?? string.Empty).Trim();
            if (!Token32Hex.IsMatch(token))
                return BadRequest(new { error = "INVALID_TOKEN_FORMAT" });

            var row = await _db.QrManifests.SingleOrDefaultAsync(x => x.Token == token, ct);
            if (row == null)
                return NotFound(new { error = "NOT_FOUND" });

            if (manifest.ValueKind != JsonValueKind.Object)
                return BadRequest(new { error = "MISSING_BODY" });

            // Guardem TAL QUAL (equivalent) el JSON rebut
            var payloadJson = manifest.GetRawText();

            // Parse per validar mínim i derivar camps
            var root = JsonNode.Parse(payloadJson) as JsonObject;
            if (root == null)
                return BadRequest(new { error = "INVALID_JSON_OBJECT" });

            var type = root["type"]?.GetValue<string>()?.Trim();
            if (!string.Equals(type, "multi", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "INVALID_TYPE", type });

            var options = root["options"] as JsonArray;
            if (options == null || options.Count == 0)
                return BadRequest(new { error = "MISSING_OPTIONS" });

            var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "itemcode", "url", "panel", "card", "wifi", "loxone"
            };

            string? derivedItemCode = null;
            string? derivedPanelCode = null;

            foreach (var optNode in options)
            {
                var opt = optNode as JsonObject;
                if (opt == null)
                    return BadRequest(new { error = "OPTION_INVALID" });

                var optType = opt["type"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(optType))
                    return BadRequest(new { error = "OPTION_TYPE_REQUIRED" });

                var optTypeNorm = optType.ToLowerInvariant();
                if (!allowedTypes.Contains(optTypeNorm))
                    return BadRequest(new { error = "OPTION_TYPE_INVALID", type = optType });

                // Derivats (primer match)
                if (derivedItemCode == null && optTypeNorm == "itemcode")
                    derivedItemCode = opt["code"]?.GetValue<string>()?.Trim();

                if (derivedPanelCode == null && optTypeNorm == "panel")
                    derivedPanelCode = opt["ref"]?.GetValue<string>()?.Trim();
            }

            row.PayloadJson = payloadJson;
            row.ItemCode = string.IsNullOrWhiteSpace(derivedItemCode) ? null : derivedItemCode;
            row.PanelCode = string.IsNullOrWhiteSpace(derivedPanelCode) ? null : derivedPanelCode;

            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                ok = true,
                token = row.Token,
                itemCode = row.ItemCode,
                panelCode = row.PanelCode
            });
        }
    }


}

