using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

/// <summary>One team's live state during a match: who is on the pitch, how tired
/// they are, and what the manager has told them to do.</summary>
public class MatchSide
{
    public MatchSide(Team team, bool isHome, TacticalStyle style, double form)
    {
        Team = team;
        IsHome = isHome;
        Style = style;
        Form = form;
        Players = team.Players.Select(p => new MatchPlayer(p)).ToList();

        ApplyFormation(team.Formation);
    }

    /// <summary>
    /// Tells each starter which job they are filling tonight. A shirt in a slot
    /// the shape means for somebody else plays below their rating, which is what
    /// makes fielding a left-back on the right, or a winger up front, cost
    /// something. Substitutes keep their own role until they come on.
    /// </summary>
    private void ApplyFormation(string formation)
    {
        foreach (var player in Players.Where(p => p.OnPitch))
            player.PlayedRole =
                FormationShape.RoleForSlot(formation, player.LineupSlot, player.NaturalRole);
    }

    public Team Team { get; }
    public bool IsHome { get; }
    public TacticalStyle Style { get; set; }
    public double Form { get; }

    public List<MatchPlayer> Players { get; }

    public IEnumerable<MatchPlayer> OnPitch => Players.Where(p => p.OnPitch);
    public IEnumerable<MatchPlayer> Bench => Players.Where(p => !p.OnPitch && !p.SentOff && !p.Injured);

    public int Goals { get; set; }

    public MatchPlayer? Keeper => OnPitch.FirstOrDefault(p => p.Position == Position.GK);

    /// <summary>Attacking output of the men currently on the pitch, after tiredness,
    /// the manager's instructions, home advantage and the day's form.</summary>
    public double Attack => Rated(
        [Position.FWD, Position.MID],
        p => (p.Shooting + p.Pace + p.Dribbling) / 3.0,
        Style switch
        {
            TacticalStyle.Attacking => EngineConstants.AttackingAttackBonus,
            TacticalStyle.Defensive => EngineConstants.DefensiveAttackPenalty,
            _ => 1.0
        },
        // A crowd lifts the attack; it does not make defenders tackle better, and
        // applying it to both would cancel out in every ratio the engine takes.
        homeBoost: true);

    public double Defense => Rated(
        [Position.DEF, Position.GK, Position.MID],
        p => (p.Defending + p.Physical + p.Pace) / 3.0,
        Style switch
        {
            TacticalStyle.Defensive => EngineConstants.DefensiveDefenseBonus,
            TacticalStyle.Attacking => EngineConstants.AttackingDefensePenalty,
            _ => 1.0
        },
        homeBoost: false);

    /// <summary>Control of the ball, which decides who gets the next attack.</summary>
    public double Possession => Rated(
        [Position.MID, Position.DEF, Position.FWD],
        p => (p.Passing * 2 + p.Dribbling) / 3.0,
        1.0,
        homeBoost: true);

    private double Rated(Position[] positions, Func<MatchPlayer, double> score, double styleFactor, bool homeBoost)
    {
        var relevant = OnPitch.Where(p => positions.Contains(p.Position)).ToList();
        if (relevant.Count == 0)
            return 1.0;

        var rating = relevant.Average(score) * styleFactor + Form;

        if (IsHome && homeBoost)
            rating *= EngineConstants.HomeAdvantage;

        // Playing a man down costs the whole side, not just one position.
        var missing = 11 - OnPitch.Count();
        if (missing > 0)
            rating *= Math.Pow(0.93, missing);

        return Math.Max(1.0, rating);
    }

    public void DrainOnPitch(double cost)
    {
        foreach (var player in OnPitch)
            player.Drain(cost);
    }
}
