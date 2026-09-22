namespace TactIQ.Domain.Entities;

public class Match
{
    public Guid Id { get; set; }
    public Guid LeagueId { get; set; }
    public League League { get; set; } = null!;

    public int WeekNumber { get; set; }

    public Guid HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;

    public Guid AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;

    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public bool IsPlayed { get; set; }

    public List<MatchEvent> Events { get; set; } = [];
    public List<TeamTactics> Tactics { get; set; } = [];
}
