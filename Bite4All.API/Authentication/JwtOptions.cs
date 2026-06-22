namespace Bite4All.API.Authentication;

public class JwtOptions
{
    public string Issuer { get; set; } = "Bite4All.API";
    public string Audience { get; set; } = "Bite4All.Client";
    public string Key { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; } = 120;
}
