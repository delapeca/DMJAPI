namespace XNDmjApi.Loxone.Options;

public class TokenPolicyOptions
{
    public int DefaultExpiresHours { get; set; } = 24;
    public int DefaultMaxUses { get; set; } = 0; // 0 = il·limitat
}