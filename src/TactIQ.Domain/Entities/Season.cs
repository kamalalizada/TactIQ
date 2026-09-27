using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

/// <summary>
/// One save file: every competition the user is following, the clubs that play
/// in them, and which club they manage.
/// </summary>
public class Season
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public GameMode GameMode { get; set; }

    /// <summary>The club the user manages; null in God Mode.</summary>
    public Guid? UserTeamId { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Team> Teams { get; set; } = [];
    public List<Competition> Competitions { get; set; } = [];

    public bool IsFinished => Competitions.Count > 0 && Competitions.All(c => c.IsFinished);
}
