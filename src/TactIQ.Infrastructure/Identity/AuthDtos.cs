namespace TactIQ.Infrastructure.Identity;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string Email, DateTime ExpiresAt);

public record AuthResult(bool Succeeded, AuthResponse? Response, IReadOnlyList<string> Errors)
{
    public static AuthResult Success(AuthResponse response) => new(true, response, []);
    public static AuthResult Failure(params string[] errors) => new(false, null, errors);
}
