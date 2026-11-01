using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TactIQ.Infrastructure.Identity;

namespace TactIQ.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, IGuestService guests) : ControllerBase
{
    /// <summary>The rate limit that keeps one visitor from opening guest after guest.</summary>
    public const string GuestRateLimit = "guest";

    /// <summary>Opens a throwaway account, so a visitor can play without registering.</summary>
    [HttpPost("guest")]
    [EnableRateLimiting(GuestRateLimit)]
    public async Task<ActionResult<AuthResponse>> Guest(CancellationToken ct) =>
        Ok(await guests.CreateAsync(ct));

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request);

        return result.Succeeded
            ? Ok(result.Response)
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request);

        return result.Succeeded
            ? Ok(result.Response)
            : Unauthorized(new { errors = result.Errors });
    }
}
