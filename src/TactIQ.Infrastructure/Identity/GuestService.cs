using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TactIQ.Application.Guests;
using TactIQ.Infrastructure.Persistence;

namespace TactIQ.Infrastructure.Identity;

public class GuestOptions
{
    public const string SectionName = "Guests";

    public int MaxAccounts { get; set; } = 50;
    public int RetentionDays { get; set; } = 7;
    public int TokenDays { get; set; } = 90;
    public int PerAddressPerHour { get; set; } = 5;
}

public interface IGuestService
{
    /// <summary>Opens a throwaway account, making room for it if guests are at the cap.</summary>
    Task<AuthResponse> CreateAsync(CancellationToken ct = default);

    /// <summary>Notes that a guest is still playing. False when the account no longer exists.</summary>
    Task<bool> TouchAsync(string userId, CancellationToken ct = default);

    /// <summary>Deletes guests untouched for the retention period, and everything they own.</summary>
    Task<int> DeleteExpiredAsync(CancellationToken ct = default);
}

/// <summary>
/// When each guest was last written down as active. Every request a guest makes
/// comes through here, so the database is written at most once per interval.
/// </summary>
public sealed class GuestActivity
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, DateTime> _recorded = new();

    public bool IsRecent(string userId, DateTime now) =>
        _recorded.TryGetValue(userId, out var at) && now - at < Interval;

    public void Record(string userId, DateTime now) => _recorded[userId] = now;

    public void Forget(string userId) => _recorded.TryRemove(userId, out _);
}

public class GuestService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IAuthService auth,
    GuestActivity activity,
    IOptions<GuestOptions> options) : IGuestService
{
    private readonly GuestOptions _options = options.Value;

    public async Task<AuthResponse> CreateAsync(CancellationToken ct = default)
    {
        var existing = await db.Users.CountAsync(u => u.IsGuest, ct);
        var toRemove = GuestRules.HowManyToRemove(existing, _options.MaxAccounts);

        if (toRemove > 0)
        {
            var longestAway = await db.Users
                .Where(u => u.IsGuest)
                .OrderBy(u => u.LastActiveAt)
                .Take(toRemove)
                .Select(u => u.Id)
                .ToListAsync(ct);

            foreach (var id in longestAway)
                await DeleteAsync(id, ct);
        }

        var address = $"guest-{Guid.NewGuid():N}@guest.tactiq.local";
        var user = new ApplicationUser
        {
            UserName = address,
            Email = address,
            IsGuest = true,
            LastActiveAt = DateTime.UtcNow
        };

        // No password: nobody can ever log in to this account, only hold its token.
        var result = await userManager.CreateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        return auth.CreateGuestToken(user);
    }

    public async Task<bool> TouchAsync(string userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (activity.IsRecent(userId, now))
            return true;

        var updated = await db.Users
            .Where(u => u.Id == userId && u.IsGuest)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.LastActiveAt, now), ct);

        if (updated == 0)
            return false;

        activity.Record(userId, now);
        return true;
    }

    public async Task<int> DeleteExpiredAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);

        var expired = await db.Users
            .Where(u => u.IsGuest && u.LastActiveAt < cutoff)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var id in expired)
            await DeleteAsync(id, ct);

        return expired.Count;
    }

    /// <summary>
    /// Seasons are not tied to users by a foreign key, so they go first; their
    /// clubs, players and matches follow by cascade. The account is matched on
    /// IsGuest as well, so a registered user can never be removed from here.
    /// </summary>
    private async Task DeleteAsync(string userId, CancellationToken ct)
    {
        var isGuest = await db.Users.AnyAsync(u => u.Id == userId && u.IsGuest, ct);
        if (!isGuest)
            return;

        await db.Seasons.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Users.Where(u => u.Id == userId && u.IsGuest).ExecuteDeleteAsync(ct);

        activity.Forget(userId);
    }
}
