using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

public class Player
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public Position Position { get; set; }
    public int ShirtNumber { get; set; }

    public int Age { get; set; }
    public string Nationality { get; set; } = string.Empty;

    public int Pace { get; set; }
    public int Shooting { get; set; }
    public int Passing { get; set; }
    public int Dribbling { get; set; }
    public int Defending { get; set; }
    public int Physical { get; set; }

    public bool IsStarter { get; set; }

    /// <summary>The player's headline rating, kept as given rather than averaged
    /// from the six attributes — a striker's low defending must not drag it down.</summary>
    public int Overall { get; set; }

    /// <summary>Condition carried between matches, 0-100. Drops while playing and
    /// recovers over the week, so a manager has to rotate a tired squad.</summary>
    public int Condition { get; set; } = 100;

    /// <summary>Matches still to sit out through injury.</summary>
    public int MatchesInjured { get; set; }

    /// <summary>Matches still to sit out through suspension.</summary>
    public int MatchesSuspended { get; set; }

    /// <summary>Yellow cards accumulated this season; a set number triggers a ban.</summary>
    public int YellowCards { get; set; }

    public bool IsAvailable => MatchesInjured == 0 && MatchesSuspended == 0;
}
