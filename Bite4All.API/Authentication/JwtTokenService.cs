using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Bite4All.Application.DTOs.Auth;
using Bite4All.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bite4All.API.Authentication;

public class JwtTokenService(IOptions<JwtOptions> options, UserManager<ApplicationUser> userManager) : IJwtTokenService
{
    public async Task<AuthResponse> CreateTokenAsync(ApplicationUser user)
    {
        var jwtOptions = options.Value;
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? user.ActorType.ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(jwtOptions.ExpiresInMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, role),
            new("actorType", user.ActorType.ToString())
        };

        if (user.HospitalityPartnerId.HasValue)
        {
            claims.Add(new Claim("hospitalityPartnerId", user.HospitalityPartnerId.Value.ToString()));
        }

        if (user.CharityOrganizationId.HasValue)
        {
            claims.Add(new Claim("charityOrganizationId", user.CharityOrganizationId.Value.ToString()));
        }

        if (user.DriverId.HasValue)
        {
            claims.Add(new Claim("driverId", user.DriverId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            jwtOptions.Issuer,
            jwtOptions.Audience,
            claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAt,
            Role = role
        };
    }
}
