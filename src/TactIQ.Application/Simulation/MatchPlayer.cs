using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

/// <summary>A player as the match sees them: their ratings degrade as they tire,
/// so the same man is worth less at 85 minutes than at kick-off.</summary>
public class MatchPlayer(Player player)
{
    public Guid Id { get; } = player.Id;
    public string Name { get; } = player.Name;
    public Position Position { get; } = player.Position;

    public bool OnPitch { get; set; } = player.IsStarter;
    public bool SentOff { get; private set; }
    public bool Injured { get; private set; }
    public int YellowCards { get; private set; }

    /// <summary>Remaining condition, 0-100. Starts at whatever the player brought
    /// into the match, so a tired man fades sooner.</summary>
    public double Stamina { get; private set; } = player.Condition;

    private readonly Player _source = player;

    public int Pace => Degraded(_source.Pace);
    public int Shooting => Degraded(_source.Shooting);
    public int Passing => Degraded(_source.Passing);
    public int Dribbling => Degraded(_source.Dribbling);
    public int Defending => Degraded(_source.Defending);
    public int Physical => Degraded(_source.Physical);
    public int Overall => _source.Overall;

    public Player Source => _source;

    /// <summary>Tiredness costs at most 25% of an attribute, and a strong player
    /// burns through it more slowly.</summary>
    private int Degraded(int attribute)
    {
        var freshness = 0.75 + 0.25 * (Stamina / 100.0);
        return Math.Max(1, (int)Math.Round(attribute * freshness));
    }

    public void Drain(double baseCost)
    {
        // Physical players last longer; the fittest lose roughly half as much.
        var resistance = 0.7 + 0.6 * (1 - _source.Physical / 100.0);
        Stamina = Math.Max(0, Stamina - baseCost * resistance);
    }

    /// <summary>Returns true when this is a second yellow, which sends the player off.</summary>
    public bool Book()
    {
        YellowCards++;
        if (YellowCards < 2)
            return false;

        SendOff();
        return true;
    }

    public void SendOff()
    {
        SentOff = true;
        OnPitch = false;
    }

    public void Injure()
    {
        Injured = true;
        OnPitch = false;
    }
}
