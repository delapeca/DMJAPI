using System.ComponentModel.DataAnnotations;

namespace XNDmjApi.Loxone.Models;

public class QrToken
{
    [Key]
    [MaxLength(64)]
    public string Token { get; set; } = default!;

    [MaxLength(16)]
    public string ActionCode { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }

    public int MaxUses { get; set; } = 0; // 0 = il·limitat
    public int UsesCount { get; set; } = 0;

    public bool Revoked { get; set; } = false;

    public DateTimeOffset? LastUsedAt { get; set; }

    [MaxLength(64)]
    public string? LastUsedIp { get; set; }
}