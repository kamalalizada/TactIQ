using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

/// <summary>A tournament inside a season: a domestic league or the Champions League.</summary>
public class Competition
{
    public Guid Id { get; set; }
    public Guid SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Country for a domestic league; null for continental competitions.</summary>
    public string? Country { get; set; }

    public CompetitionKind Kind { get; set; }

    public int TeamCount { get; set; }

    /// <summary>League week, or the round a knockout has reached.</summary>
    public int CurrentWeek { get; set; } = 1;

    public CupRound? CurrentRound { get; set; }

    public Guid? WinnerTeamId { get; set; }

    /// <summary>Which clubs take part. The Champions League draws from every league.</summary>
    public List<CompetitionEntry> Entries { get; set; } = [];
    public List<Match> Matches { get; set; } = [];

    public int TotalWeeks => Kind == CompetitionKind.League ? 2 * (TeamCount - 1) : 0;

    public bool IsFinished => Kind == CompetitionKind.League
        ? CurrentWeek > TotalWeeks
        : WinnerTeamId is not null;
}

/// <summary>Joins a club to a competition it plays in.</summary>
public class CompetitionEntry
{
    public Guid Id { get; set; }

    public Guid CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;

    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    /// <summary>Group letter during a Champions League group stage.</summary>
    public string? GroupName { get; set; }

    public bool Eliminated { get; set; }
}
