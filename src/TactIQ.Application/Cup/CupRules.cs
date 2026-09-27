using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Cup;

public record CupTie(Guid TieId, CupRound Round, Guid FirstLegHomeTeamId, Guid FirstLegAwayTeamId);

/// <summary>How a tie ended: the winner, and whether it needed spot kicks.</summary>
public record TieOutcome(Guid WinnerTeamId, bool DecidedOnPenalties, int HomeAggregate, int AwayAggregate);

public static class CupRules
{
    /// <summary>Clubs that qualify for the Champions League.</summary>
    public const int EntrantCount = 16;

    public const int GroupCount = 4;
    public const int GroupSize = EntrantCount / GroupCount;

    /// <summary>How many of each group go through to the knockout rounds.</summary>
    public const int QualifiersPerGroup = 2;

    public static CupRound? NextRound(CupRound round) => round switch
    {
        CupRound.GroupStage => CupRound.QuarterFinal,
        CupRound.RoundOf16 => CupRound.QuarterFinal,
        CupRound.QuarterFinal => CupRound.SemiFinal,
        CupRound.SemiFinal => CupRound.Final,
        _ => null
    };

    /// <summary>The final is a one-off; earlier rounds are home and away.</summary>
    public static bool IsTwoLegged(CupRound round) => round != CupRound.Final;

    /// <summary>
    /// Splits the qualifiers into groups, keeping clubs from one country apart
    /// where possible so the draw does not put three English sides together.
    /// </summary>
    public static IReadOnlyDictionary<string, List<Guid>> DrawGroups(
        IReadOnlyList<Team> teams,
        Random random)
    {
        if (teams.Count != EntrantCount)
            throw new ArgumentException($"The group stage needs exactly {EntrantCount} teams.", nameof(teams));

        // Deal round-robin from each country in turn, so a country's clubs land
        // in different groups before any group takes a second one.
        var byCountry = teams
            .GroupBy(t => t.Country)
            .OrderByDescending(g => g.Count())
            .Select(g => new Queue<Team>(g.OrderBy(_ => random.Next())))
            .ToList();

        var groups = Enumerable.Range(0, GroupCount)
            .ToDictionary(i => ((char)('A' + i)).ToString(), _ => new List<Guid>());

        var names = groups.Keys.ToList();
        var countryOf = teams.ToDictionary(t => t.Id, t => t.Country);

        while (byCountry.Any(q => q.Count > 0))
        {
            foreach (var queue in byCountry.Where(q => q.Count > 0).ToList())
            {
                var team = queue.Peek();

                var open = names.Where(n => groups[n].Count < GroupSize).ToList();
                if (open.Count == 0)
                    break;

                // Prefer a group with none of this country in it yet.
                var target = open
                    .OrderBy(n => groups[n].Count(id => countryOf[id] == team.Country))
                    .ThenBy(n => groups[n].Count)
                    .ThenBy(_ => random.Next())
                    .First();

                groups[target].Add(queue.Dequeue().Id);
            }
        }

        return groups;
    }

    /// <summary>Pairs the surviving teams at random for the given round.</summary>
    public static IReadOnlyList<CupTie> Draw(IReadOnlyList<Guid> teamIds, CupRound round, Random random)
    {
        if (teamIds.Count < 2 || teamIds.Count % 2 != 0)
            throw new ArgumentException("A cup round needs an even number of teams.", nameof(teamIds));

        var shuffled = teamIds.OrderBy(_ => random.Next()).ToList();
        var ties = new List<CupTie>(shuffled.Count / 2);

        for (var i = 0; i < shuffled.Count; i += 2)
            ties.Add(new CupTie(Guid.NewGuid(), round, shuffled[i], shuffled[i + 1]));

        return ties;
    }

    /// <summary>
    /// Works out who goes through. Aggregate score decides it; level ties are
    /// settled by penalties, which the caller records on the deciding match.
    /// </summary>
    public static TieOutcome Resolve(IReadOnlyList<Match> legs)
    {
        if (legs.Count == 0)
            throw new ArgumentException("A tie needs at least one leg.", nameof(legs));

        var firstLeg = legs.OrderBy(m => m.Leg ?? 1).First();
        var homeTeamId = firstLeg.HomeTeamId;
        var awayTeamId = firstLeg.AwayTeamId;

        var homeAggregate = 0;
        var awayAggregate = 0;

        foreach (var leg in legs)
        {
            if (leg.HomeTeamId == homeTeamId)
            {
                homeAggregate += leg.HomeGoals;
                awayAggregate += leg.AwayGoals;
            }
            else
            {
                homeAggregate += leg.AwayGoals;
                awayAggregate += leg.HomeGoals;
            }
        }

        if (homeAggregate != awayAggregate)
        {
            return new TieOutcome(
                homeAggregate > awayAggregate ? homeTeamId : awayTeamId,
                DecidedOnPenalties: false,
                homeAggregate,
                awayAggregate);
        }

        // Level on aggregate: whoever won the shoot-out in the final leg goes through.
        var decider = legs.OrderByDescending(m => m.Leg ?? 1).First();

        if (decider is { HomePenalties: { } homePens, AwayPenalties: { } awayPens })
        {
            var shootoutWinner = homePens > awayPens ? decider.HomeTeamId : decider.AwayTeamId;
            return new TieOutcome(shootoutWinner, DecidedOnPenalties: true, homeAggregate, awayAggregate);
        }

        throw new InvalidOperationException("The tie is level and no shoot-out was recorded.");
    }

    /// <summary>
    /// Plays a shoot-out. Five kicks each, then sudden death; the better takers
    /// and a sharper keeper tilt it without ever making it certain.
    /// </summary>
    public static (int Home, int Away) TakePenalties(Team home, Team away, Random random)
    {
        var homeSkill = ShootoutSkill(home);
        var awaySkill = ShootoutSkill(away);

        int homeScore = 0, awayScore = 0;

        for (var kick = 0; kick < 5; kick++)
        {
            if (random.NextDouble() < homeSkill) homeScore++;
            if (random.NextDouble() < awaySkill) awayScore++;
        }

        // Sudden death, capped so a freak run cannot loop forever.
        for (var extra = 0; extra < 20 && homeScore == awayScore; extra++)
        {
            if (random.NextDouble() < homeSkill) homeScore++;
            if (random.NextDouble() < awaySkill) awayScore++;
        }

        if (homeScore == awayScore)
            homeScore++;

        return (homeScore, awayScore);
    }

    private static double ShootoutSkill(Team team)
    {
        var takers = team.Players
            .Where(p => p.IsStarter && p.Position != Position.GK)
            .ToList();

        if (takers.Count == 0)
            return 0.7;

        var composure = takers.Average(p => (p.Shooting + p.Physical) / 2.0);

        // Real shoot-outs convert roughly three quarters of the time.
        return Math.Clamp(0.55 + composure / 250.0, 0.55, 0.92);
    }
}
