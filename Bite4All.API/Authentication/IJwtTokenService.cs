using Bite4All.Application.DTOs.Auth;
using Bite4All.Infrastructure.Identity;

namespace Bite4All.API.Authentication;

public interface IJwtTokenService
{
    Task<AuthResponse> CreateTokenAsync(ApplicationUser user);
}
