namespace TactIQ.Infrastructure.Identity;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TactIQ";
    public string Audience { get; set; } = "TactIQ";
    public string Key { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 12;
}
