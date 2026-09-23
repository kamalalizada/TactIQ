using TactIQ.Domain.Entities;

namespace TactIQ.Application.Simulation;

/// <summary>Carries what a match did to the players back into the squad: how tired
/// they finished, who picked up a ban, and who is out injured.</summary>
public static class SquadAftermath
{
    private static readonly Random Random = new();

    public static void Apply(IEnumerable<Player> squad, IReadOnlyList<PlayerAftermath> aftermath)
    {
        var byId = squad.ToDictionary(p => p.Id);

        foreach (var result in aftermath)
        {
            if (!byId.TryGetValue(result.PlayerId, out var player))
                continue;

            // A floor keeps a heavily used player weak but still usable, rather
            // than hitting zero and becoming unplayable for the rest of the season.
            player.Condition = Math.Clamp(result.Condition, 40, 100);
            player.YellowCards += result.YellowCards;

            if (result.SentOff)
                player.MatchesSuspended += EngineConstants.MatchesBannedForRedCard;

            // A run of bookings earns a ban of its own.
            if (player.YellowCards >= EngineConstants.YellowCardsForBan)
            {
                player.MatchesSuspended += 1;
                player.YellowCards -= EngineConstants.YellowCardsForBan;
            }

            if (result.Injured)
                player.MatchesInjured += Random.Next(1, 5);
        }
    }

    /// <summary>Called once a week has been played: bans and knocks tick down and
    /// everyone recovers some condition.</summary>
    public static void RecoverWeek(IEnumerable<Player> squad)
    {
        foreach (var player in squad)
        {
            if (player.MatchesSuspended > 0)
                player.MatchesSuspended--;

            if (player.MatchesInjured > 0)
                player.MatchesInjured--;

            player.Condition = Math.Min(100, player.Condition + EngineConstants.ConditionRecoveryPerWeek);
        }
    }
}
