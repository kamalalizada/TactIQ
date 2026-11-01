using Microsoft.AspNetCore.Identity;

namespace TactIQ.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>A throwaway account with no password, deleted once it goes untouched.</summary>
    public bool IsGuest { get; set; }

    /// <summary>When a guest was last seen. Always null for a registered user.</summary>
    public DateTime? LastActiveAt { get; set; }
}
