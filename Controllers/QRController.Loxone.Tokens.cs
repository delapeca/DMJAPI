// Controllers/QRController.Loxone.Tokens.cs
//
// DMJAPI · Port XNLoxoneAPI · Tokens admin (partial QRController)
//
// Requereix X-Api-Key (Admin:ApiKey) a tots els endpoints d'aquest fitxer.
// Notes:
//  - Reutilitza helpers del fitxer QRController.Loxone.cs: IsAdminAuthorized(), EnsureLoxoneDbAsync().
//  - Reutilitza DI: _loxDb i _tokens (TokenService).

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XNDmjApi.Loxone.Models;

namespace XNDmjApi.Controllers
{
    public partial class QRController : ControllerBase
    {
        // ==========================================================
        // DTOs
        // ==========================================================
        public class CreateTokenRequest
        {
            public string? ActionCode { get; set; }
            public int? ExpiresHours { get; set; }   // null => default policy; 0 o <0 => sense expiració
            public int? MaxUses { get; set; }        // null => default policy; 0 => il·limitat
        }

        public class TokenView
        {
            public string Token { get; set; } = default!;
            public string ActionCode { get; set; } = default!;
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset? ExpiresAt { get; set; }
            public int MaxUses { get; set; }
            public int UsesCount { get; set; }
            public bool Revoked { get; set; }
            public DateTimeOffset? LastUsedAt { get; set; }
            public string? LastUsedIp { get; set; }
        }

        private static TokenView ToView(QrToken t) => new TokenView
        {
            Token = t.Token,
            ActionCode = t.ActionCode,
            CreatedAt = t.CreatedAt,
            ExpiresAt = t.ExpiresAt,
            MaxUses = t.MaxUses,
            UsesCount = t.UsesCount,
            Revoked = t.Revoked,
            LastUsedAt = t.LastUsedAt,
            LastUsedIp = t.LastUsedIp
        };

        // ==========================================================
        // ADMIN: POST /api/loxone/tokens
        // ==========================================================
        [HttpPost("~/api/loxone/tokens")]
        public async Task<IActionResult> Admin_CreateToken([FromBody] CreateTokenRequest req, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var actionCode = (req?.ActionCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(actionCode))
                return BadRequest(new { error = "MISSING_ACTION_CODE" });

            // Valida que l'acció existeix
            var exists = await _loxDb.LoxoneActions.AnyAsync(a => a.Code == actionCode, ct);
            if (!exists)
                return NotFound(new { error = "ACTION_NOT_FOUND", actionCode });

            // Crea token segons política / overrides
            var t = await _tokens.CreateAsync(
                actionCode,
                req?.ExpiresHours,
                req?.MaxUses,
                ct
            );

            return Ok(ToView(t));
        }

        // ==========================================================
        // ADMIN: GET /api/loxone/tokens  (limit 200)
        // ==========================================================
        [HttpGet("~/api/loxone/tokens")]
        public async Task<IActionResult> Admin_ListTokens([FromQuery] int? take, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            int n = take.HasValue ? Math.Clamp(take.Value, 1, 200) : 200;

            // SQLite + EF Core: DateTimeOffset no es pot traduir a ORDER BY en SQL.
            // Per llistats petits (n <= 200) fem l'ordenació en memòria.
            var all = await _loxDb.QrTokens
                .AsNoTracking()
                .Select(x => new TokenView
                {
                    Token = x.Token,
                    ActionCode = x.ActionCode,
                    CreatedAt = x.CreatedAt,
                    ExpiresAt = x.ExpiresAt,
                    MaxUses = x.MaxUses,
                    UsesCount = x.UsesCount,
                    Revoked = x.Revoked,
                    LastUsedAt = x.LastUsedAt,
                    LastUsedIp = x.LastUsedIp
                })
                .ToListAsync(ct);

            var items = all
                .OrderByDescending(x => x.CreatedAt.UtcTicks)
                .Take(n)
                .ToList();

            return Ok(items);
        }

        // ==========================================================
        // ADMIN: GET /api/loxone/tokens/{token}
        // ==========================================================
        [HttpGet("~/api/loxone/tokens/{token}")]
        public async Task<IActionResult> Admin_GetToken([FromRoute] string token, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var key = (token ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "MISSING_TOKEN" });

            var t = await _loxDb.QrTokens.AsNoTracking().SingleOrDefaultAsync(x => x.Token == key, ct);
            if (t is null) return NotFound(new { error = "TOKEN_NOT_FOUND" });

            return Ok(ToView(t));
        }

        // ==========================================================
        // ADMIN: POST /api/loxone/tokens/{token}/revoke
        // ==========================================================
        [HttpPost("~/api/loxone/tokens/{token}/revoke")]
        public async Task<IActionResult> Admin_RevokeToken([FromRoute] string token, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var key = (token ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "MISSING_TOKEN" });

            var t = await _loxDb.QrTokens.SingleOrDefaultAsync(x => x.Token == key, ct);
            if (t is null) return NotFound(new { error = "TOKEN_NOT_FOUND" });

            if (!t.Revoked)
            {
                t.Revoked = true;
                await _loxDb.SaveChangesAsync(ct);
            }

            return Ok(ToView(t));
        }
    }
}