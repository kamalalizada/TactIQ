using Microsoft.AspNetCore.Identity;

namespace TactIQ.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
