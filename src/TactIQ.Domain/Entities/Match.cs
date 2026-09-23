using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

public class Match
{
    public Guid Id { get; set; }
    public Guid LeagueId { get; set; }
    public League League { get; set; } = null!;

    public CompetitionType Competition { get; set; } = CompetitionType.League;

    /// <summary>League week, or the leg number for a cup tie.</summary>
    public int WeekNumber { get; set; }

    /// <summary>Set on cup ties only.</summary>
    public CupRound? Round { get; set; }

    /// <summary>1 or 2 for a two-legged tie; the final is a single match.</summary>
    public int? Leg { get; set; }

    /// <summary>Ties the two legs of a cup tie together.</summary>
    public Guid? TieId { get; set; }

    public Guid HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;

    public Guid AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;

    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public bool IsPlayed { get; set; }

    /// <summary>Filled when a tie had to be settled from the spot.</summary>
    public int? HomePenalties { get; set; }
    public int? AwayPenalties { get; set; }

    public List<MatchEvent> Events { get; set; } = [];
    public List<TeamTactics> Tactics { get; set; } = [];

    public bool WentToPenalties => HomePenalties is not null && AwayPenalties is not null;
}
