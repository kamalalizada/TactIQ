namespace TactIQ.Domain.Entities;

/// <summary>
/// What happened in one season of a career.
///
/// A save that rolls on keeps only its current table; the season before it is
/// gone the moment the fixtures are redrawn. This is what a manager looks back
/// on — the titles he won, the year he finished fourth, the season his club
/// went out of Europe in the group stage.
/// </summary>
public class SeasonRecord
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    /// <summary>Which season of the career this was, counting from one.</summary>
    public int Year { get; set; }

    /// <summary>The competition this record is for; a career has one per league.</summary>
    public string CompetitionName { get; set; } = string.Empty;

    public string? Country { get; set; }

    /// <summary>Named as they were, since a club may be renamed or a save edited.</summary>
    public Guid? ChampionTeamId { get; set; }
    public string? ChampionTeamName { get; set; }

    /// <summary>Where the managed club finished; null if the manager was not in this league.</summary>
    public int? UserRank { get; set; }
    public int? UserPoints { get; set; }

    /// <summary>What the club was worth at the end of the year, for a career graph.</summary>
    public decimal? UserBudget { get; set; }

    public DateTime RecordedAt { get; set; }
}
