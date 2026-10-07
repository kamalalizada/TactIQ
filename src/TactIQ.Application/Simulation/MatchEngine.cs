using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

public class MatchEngine : IMatchEngine
{
    private readonly Random _random;
    private readonly AttackResolver _attacks;

    public MatchEngine(Random? random = null)
    {
        _random = random ?? Random.Shared;
        _attacks = new AttackResolver(_random);
    }

    public MatchSimulationResult SimulateInstant(Team home, Team away)
    {
        var homeSide = CreateSide(home, isHome: true, TacticalStyle.Balanced);
        var awaySide = CreateSide(away, isHome: false, TacticalStyle.Balanced);

        var events = new List<SimulatedEvent>();

        for (var minute = 1; minute <= EngineConstants.RegularTimeMinutes; minute++)
            events.AddRange(PlayMinute(homeSide, awaySide, minute));

        return new MatchSimulationResult(
            homeSide.Goals,
            awaySide.Goals,
            events,
            Aftermath(homeSide).Concat(Aftermath(awaySide)).ToList());
    }

    /// <summary>Builds the live view of a team, picking a fit eleven and rolling
    /// the day's form.</summary>
    public MatchSide CreateSide(Team team, bool isHome, TacticalStyle style)
    {
        var side = new MatchSide(team, isHome, style, RandomForm());
        EnsureValidEleven(side);
        return side;
    }

    /// <summary>Scales a FIFA-style 50-100 rating into an exponentially weighted score.</summary>
    internal static double Scale(double rating) => Math.Pow(Math.Max(1.0, rating - 45.0), 2.5);

    /// <summary>Runs one minute for both sides and returns whatever happened.</summary>
    public IReadOnlyList<SimulatedEvent> PlayMinute(MatchSide home, MatchSide away, int minute)
    {
        var events = new List<SimulatedEvent>();

        home.DrainOnPitch(EngineConstants.StaminaPerMinute);
        away.DrainOnPitch(EngineConstants.StaminaPerMinute);

        // Attacks arrive at a steady rate; possession decides whose they are.
        var attackChance = EngineConstants.BaseAttacksPerMatch / EngineConstants.RegularTimeMinutes;
        if (_random.NextDouble() >= attackChance)
            return events;

        // Use power scaling to amplify the difference between strong and weak teams.
        // A linear ratio (80 vs 70) makes every match a coin toss (53% vs 47%).
        // Subtracting 45 stretches the 50-100 scale, and cubing it forces the better team to dominate.
        var homeWeight = Scale(home.Possession);
        var awayWeight = Scale(away.Possession);
        var total = homeWeight + awayWeight;
        
        var homeHasBall = _random.NextDouble() < homeWeight / total;

        var (attacking, defending) = homeHasBall ? (home, away) : (away, home);

        foreach (var outcome in _attacks.Resolve(attacking, defending, minute))
        {
            // Cards and injuries belong to whoever they happened to, not to the
            // side that was attacking.
            var owner = outcome.Type is MatchEventType.Goal or MatchEventType.ShotOffTarget
                ? attacking
                : SideOf(outcome.Player, home, away) ?? attacking;

            events.Add(new SimulatedEvent(
                minute,
                outcome.Type,
                owner.Team.Id,
                outcome.Player?.Id,
                outcome.Assist?.Id,
                outcome.Description));
        }

        return events;
    }

    public IReadOnlyList<PlayerAftermath> Aftermath(MatchSide side) =>
        side.Players
            .Where(p => p.OnPitch || p.SentOff || p.Injured || p.Stamina < p.Source.Condition)
            .Select(p => new PlayerAftermath(
                p.Id,
                (int)Math.Round(p.Stamina),
                p.Injured,
                p.SentOff,
                p.YellowCards))
            .ToList();

    public TeamStrength ComputeStrength(Team team, bool isHome, TacticalStyle style, double formModifier)
    {
        var side = new MatchSide(team, isHome, style, formModifier);
        EnsureValidEleven(side);
        return new TeamStrength(side.Attack, side.Defense);
    }

    public double GoalChancePerMinute(TeamStrength attacking, TeamStrength defending)
    {
        var attWeight = Scale(attacking.Attack);
        var defWeight = Scale(defending.Defense);
        var pressure = attWeight / (attWeight + defWeight);
        
        var attacksPerMinute = EngineConstants.BaseAttacksPerMatch / EngineConstants.RegularTimeMinutes;
        return attacksPerMinute * pressure * EngineConstants.BaseChanceConversion;
    }

    private static MatchSide? SideOf(MatchPlayer? player, MatchSide home, MatchSide away)
    {
        if (player is null)
            return null;

        return home.Players.Any(p => p.Id == player.Id) ? home : away;
    }

    /// <summary>Injured and suspended players cannot start, so promote fit
    /// replacements before kick-off.</summary>
    private static void EnsureValidEleven(MatchSide side)
    {
        foreach (var unavailable in side.Players.Where(p => p.OnPitch && !p.Source.IsAvailable).ToList())
        {
            var replacement = side.Players
                .Where(p => !p.OnPitch && p.Source.IsAvailable)
                .OrderByDescending(p => p.Position == unavailable.Position)
                .ThenByDescending(p => p.Overall)
                .FirstOrDefault();

            if (replacement is null)
                continue;

            unavailable.OnPitch = false;
            replacement.OnPitch = true;
            // The replacement steps into the job, not just the shirt: whoever
            // covers an injured keeper goes in goal.
            replacement.PlayedRole = unavailable.PlayedRole;
        }
    }

    private double RandomForm() =>
        (_random.NextDouble() * 2 - 1) * EngineConstants.FormSwing;
}
