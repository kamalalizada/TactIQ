using Microsoft.AspNetCore.Mvc;
using TactIQ.Infrastructure.Identity;

namespace TactIQ.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
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
