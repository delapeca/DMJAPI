using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using XNDmjApi.Loxone.Data;
using XNDmjApi.Loxone.Models;
using XNDmjApi.Loxone.Options;

namespace XNDmjApi.Loxone.Services;

public class TokenService
{
    private readonly AppDbContext _db;
    private readonly TokenPolicyOptions _policy;

    public TokenService(AppDbContext db, IOptions<TokenPolicyOptions> policy)
    {
        _db = db;
        _policy = policy.Value;
    }

    public async Task<QrToken> CreateAsync(string actionCode, int? expiresHours, int? maxUses, CancellationToken ct)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

        var t = new QrToken
        {
            Token = token,
            ActionCode = actionCode,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = (expiresHours ?? _policy.DefaultExpiresHours) > 0
                ? DateTimeOffset.UtcNow.AddHours(expiresHours ?? _policy.DefaultExpiresHours)
                : null,
            MaxUses = maxUses ?? _policy.DefaultMaxUses,
            UsesCount = 0,
            Revoked = false
        };

        _db.QrTokens.Add(t);
        await _db.SaveChangesAsync(ct);
        return t;
    }

    public async Task<(QrToken? token, string? error)> ValidateForUseAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (null, "MISSING_TOKEN");

        var t = await _db.QrTokens.SingleOrDefaultAsync(x => x.Token == token.Trim(), ct);
        if (t is null) return (null, "TOKEN_NOT_FOUND");
        if (t.Revoked) return (null, "TOKEN_REVOKED");

        var now = DateTimeOffset.UtcNow;
        if (t.ExpiresAt != null && t.ExpiresAt <= now) return (null, "TOKEN_EXPIRED");

        if (t.MaxUses > 0 && t.UsesCount >= t.MaxUses) return (null, "TOKEN_MAX_USES");

        return (t, null);
    }

    public async Task MarkUsedAsync(QrToken t, string? ip, CancellationToken ct)
    {
        t.UsesCount++;
        t.LastUsedAt = DateTimeOffset.UtcNow;
        t.LastUsedIp = ip;
        await _db.SaveChangesAsync(ct);
    }
}