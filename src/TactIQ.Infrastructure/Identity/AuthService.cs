using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace TactIQ.Infrastructure.Identity;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    AuthResponse CreateGuestToken(ApplicationUser user);
}

public class AuthService(
    UserManager<ApplicationUser> userManager,
    IOptions<JwtOptions> jwtOptions,
    IOptions<GuestOptions> guestOptions) : IAuthService
{
    /// <summary>Marks a token as a guest's, so a request knows to check the account still exists.</summary>
    public const string GuestClaim = "guest";

    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly GuestOptions _guests = guestOptions.Value;

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        return result.Succeeded
            ? AuthResult.Success(CreateToken(user))
            : AuthResult.Failure(result.Errors.Select(Describe).Distinct().ToArray());
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        return user is not null && await userManager.CheckPasswordAsync(user, request.Password)
            ? AuthResult.Success(CreateToken(user))
            : AuthResult.Failure("E-poçt və ya parol səhvdir.");
    }

    /// <summary>
    /// A guest has no password to log back in with, so the token has to outlive
    /// the save. It can run long because it is not the last word: a request from
    /// a guest whose account has been deleted is refused regardless.
    /// </summary>
    public AuthResponse CreateGuestToken(ApplicationUser user) =>
        CreateToken(user, DateTime.UtcNow.AddDays(_guests.TokenDays), new Claim(GuestClaim, "true"));

    private AuthResponse CreateToken(ApplicationUser user) =>
        CreateToken(user, DateTime.UtcNow.AddHours(_jwt.ExpiryHours));

    private static string Describe(IdentityError error) => error.Code switch
    {
        "DuplicateEmail" or "DuplicateUserName" => "Bu e-poçt artıq qeydiyyatdan keçib.",
        "InvalidEmail" or "InvalidUserName" => "E-poçt ünvanı düzgün deyil.",
        "PasswordTooShort" => "Parol ən azı 6 simvol olmalıdır.",
        _ => error.Description
    };

    private AuthResponse CreateToken(ApplicationUser user, DateTime expiresAt, params Claim[] extra)
    {
        Claim[] claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            .. extra
        ];

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), user.Email!, expiresAt);
    }
}
