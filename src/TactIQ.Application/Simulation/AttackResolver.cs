using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

public record AttackOutcome(
    MatchEventType Type,
    MatchPlayer? Player,
    MatchPlayer? Assist,
    string Description);

/// <summary>Plays out a single attacking move: who carries it, whether a chance
/// appears, who shoots, and whether the keeper keeps it out.</summary>
public class AttackResolver(Random random)
{
    public IReadOnlyList<AttackOutcome> Resolve(MatchSide attacking, MatchSide defending, int minute)
    {
        var outcomes = new List<AttackOutcome>();

        if (TryFoul(attacking, defending, minute, outcomes))
            return outcomes;

        // Does the move survive the defence and become a shot?
        var pressure = attacking.Attack / (attacking.Attack + defending.Defense);
        if (random.NextDouble() > pressure * EngineConstants.BaseChanceConversion * 2)
            return outcomes;

        var shooter = PickShooter(attacking);
        if (shooter is null)
            return outcomes;

        var assist = PickAssist(attacking, shooter);
        var keeper = defending.Keeper;

        // Shot quality against the keeper's ability to deal with it.
        var shotQuality = (shooter.Shooting * 0.7 + shooter.Dribbling * 0.3) / 100.0;
        var keeperQuality = keeper is null ? 0.35 : keeper.Defending / 100.0;
        var scoreChance = shotQuality * 0.55 * (1 - keeperQuality * 0.55);

        var roll = random.NextDouble();

        if (roll < scoreChance)
        {
            attacking.Goals++;
            outcomes.Add(new AttackOutcome(MatchEventType.Goal, shooter, assist,
                assist is null
                    ? $"{shooter.Name} scores for {attacking.Team.Name}"
                    : $"{shooter.Name} scores for {attacking.Team.Name}, assisted by {assist.Name}"));
        }
        else if (roll < scoreChance + 0.30 && keeper is not null)
        {
            outcomes.Add(new AttackOutcome(MatchEventType.Save, keeper, null,
                $"{keeper.Name} saves from {shooter.Name}"));
        }
        else
        {
            outcomes.Add(new AttackOutcome(MatchEventType.ShotOffTarget, shooter, null,
                $"{shooter.Name} shoots wide"));
        }

        return outcomes;
    }

    private bool TryFoul(MatchSide attacking, MatchSide defending, int minute, List<AttackOutcome> outcomes)
    {
        if (random.NextDouble() >= EngineConstants.FoulChancePerAttack)
            return false;

        var offender = PickWeighted(defending.OnPitch.Where(p => p.Position != Position.GK).ToList(),
            p => p.Position == Position.DEF ? 3.0 : p.Position == Position.MID ? 2.0 : 1.0);

        if (offender is null)
            return false;

        var roll = random.NextDouble();

        if (roll < EngineConstants.StraightRedFromFoul)
        {
            offender.SendOff();
            outcomes.Add(new AttackOutcome(MatchEventType.RedCard, offender, null,
                $"{offender.Name} is sent off"));
            return true;
        }

        if (roll < EngineConstants.YellowFromFoul)
        {
            // A player already on a booking is far more careful, so second yellows
            // stay rare rather than following automatically from a busy match.
            if (offender.YellowCards > 0 && random.NextDouble() > EngineConstants.SecondYellowChance)
                return true;

            var secondYellow = offender.Book();
            outcomes.Add(new AttackOutcome(
                secondYellow ? MatchEventType.RedCard : MatchEventType.YellowCard,
                offender, null,
                secondYellow
                    ? $"{offender.Name} is sent off for a second booking"
                    : $"{offender.Name} is booked"));
            return true;
        }

        // A hard challenge that hurts someone rather than earning a card.
        if (random.NextDouble() < EngineConstants.InjuryChancePerAttack)
        {
            var victim = PickWeighted(attacking.OnPitch.ToList(), _ => 1.0);
            if (victim is not null)
            {
                victim.Injure();
                outcomes.Add(new AttackOutcome(MatchEventType.Injury, victim, null,
                    $"{victim.Name} goes down injured"));
            }
        }

        return true;
    }

    private MatchPlayer? PickShooter(MatchSide side) =>
        PickWeighted(side.OnPitch.Where(p => p.Position != Position.GK).ToList(), p =>
        {
            var positional = p.Position switch
            {
                Position.FWD => 5.0,
                Position.MID => 2.2,
                _ => 0.4
            };
            return positional * (p.Shooting / 60.0);
        });

    private MatchPlayer? PickAssist(MatchSide side, MatchPlayer shooter)
    {
        // Not every goal is assisted.
        if (random.NextDouble() < 0.28)
            return null;

        var candidates = side.OnPitch
            .Where(p => p.Id != shooter.Id && p.Position != Position.GK)
            .ToList();

        return PickWeighted(candidates, p =>
        {
            var positional = p.Position switch
            {
                Position.MID => 4.0,
                Position.FWD => 2.5,
                _ => 1.0
            };
            return positional * (p.Passing / 60.0);
        });
    }

    private MatchPlayer? PickWeighted(List<MatchPlayer> candidates, Func<MatchPlayer, double> weight)
    {
        if (candidates.Count == 0)
            return null;

        var weights = candidates.Select(c => Math.Max(0.01, weight(c))).ToList();
        var target = random.NextDouble() * weights.Sum();

        for (var i = 0; i < candidates.Count; i++)
        {
            target -= weights[i];
            if (target <= 0)
                return candidates[i];
        }

        return candidates[^1];
    }
}
