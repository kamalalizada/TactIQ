using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

public class League
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public string SeasonName { get; set; } = string.Empty;
    public int TeamCount { get; set; } = 8;
    public int CurrentWeek { get; set; } = 1;

    public GameMode GameMode { get; set; }
    public Guid? UserTeamId { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Team> Teams { get; set; } = [];
    public List<Match> Matches { get; set; } = [];

    public int TotalWeeks => 2 * (TeamCount - 1);
    public bool IsFinished => CurrentWeek > TotalWeeks;
}
