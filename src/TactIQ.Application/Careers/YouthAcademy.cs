using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Careers;

/// <summary>
/// The players a club brings through when somebody retires.
///
/// Without this a squad would only ever shrink: every season a few careers end
/// and nothing replaces them, until a club cannot name a bench. A youth player
/// is deliberately raw — worse than the man he replaces, and years from his
/// best — so a manager who loses a good player has still lost something.
/// </summary>
public static class YouthAcademy
{
    /// <summary>How far below the squad's standard a youth player starts.</summary>
    private const int Shortfall = 11;

    /// <summary>Nobody comes through below this; a professional has to be able to play.</summary>
    private const int FloorRating = 54;

    private static readonly string[] FirstNames =
    [
        "Arda", "Emre", "Kerem", "Yusuf", "Mehmet", "Tunahan", "Berkay", "Ali",
        "Lucas", "Matteo", "Diego", "Rafael", "Bruno", "Enzo", "Thiago", "Marco",
        "Liam", "Owen", "Harry", "Jack", "Noah", "Finley", "Leo", "Max",
        "Samuel", "Daniel", "Antoine", "Hugo", "Youssef", "Omar", "Ibrahim"
    ];

    private static readonly string[] LastNames =
    [
        "Yilmaz", "Demir", "Kaya", "Celik", "Sahin", "Ozturk", "Arslan", "Dogan",
        "Silva", "Santos", "Oliveira", "Pereira", "Costa", "Rocha", "Moreno",
        "Walker", "Hughes", "Palmer", "Brooks", "Reid", "Shaw", "Doyle",
        "Mendy", "Laurent", "Dubois", "Haddad", "Nasri", "Keita", "Traore"
    ];

    /// <summary>
    /// Brings a player through to fill a gap. He takes the retiring player's
    /// position and shirt, so the squad keeps its shape and its numbering.
    /// </summary>
    public static Player Promote(Team club, Player replacing, Random random)
    {
        var standard = club.Players.Count > 0
            ? (int)Math.Round(club.Players.Average(p => p.Overall))
            : replacing.Overall;

        var overall = Math.Max(FloorRating, standard - Shortfall + random.Next(0, 5));
        var age = random.Next(17, 20);

        var player = new Player
        {
            Id = Guid.NewGuid(),
            TeamId = club.Id,
            Name = Name(random),
            Position = replacing.Position,
            Role = replacing.Role,
            ShirtNumber = replacing.ShirtNumber,
            Age = age,
            Nationality = replacing.Nationality,
            Overall = overall,

            // A substitute until the manager picks him, like any new arrival.
            IsStarter = false,
            LineupSlot = 0,
            Condition = 100
        };

        GiveAttributes(player, overall, random);

        return player;
    }

    /// <summary>
    /// Attributes around the player's rating, weighted towards what his
    /// position asks for — a young centre-back defends before he passes.
    /// </summary>
    private static void GiveAttributes(Player player, int overall, Random random)
    {
        int Near(int bias) => Math.Clamp(overall + bias + random.Next(-4, 5), 25, 92);

        switch (player.Position)
        {
            case Position.GK:
                player.Defending = Near(4);
                player.Physical = Near(2);
                player.Passing = Near(-6);
                player.Pace = Near(-2);
                player.Shooting = Near(-25);
                player.Dribbling = Near(-14);
                break;

            case Position.DEF:
                player.Defending = Near(5);
                player.Physical = Near(3);
                player.Pace = Near(0);
                player.Passing = Near(-3);
                player.Dribbling = Near(-4);
                player.Shooting = Near(-16);
                break;

            case Position.MID:
                player.Passing = Near(4);
                player.Dribbling = Near(3);
                player.Pace = Near(0);
                player.Defending = Near(-3);
                player.Physical = Near(-2);
                player.Shooting = Near(-2);
                break;

            default:
                player.Shooting = Near(4);
                player.Pace = Near(4);
                player.Dribbling = Near(3);
                player.Physical = Near(-2);
                player.Passing = Near(-5);
                player.Defending = Near(-22);
                break;
        }
    }

    private static string Name(Random random) =>
        $"{FirstNames[random.Next(FirstNames.Length)]} {LastNames[random.Next(LastNames.Length)]}";
}
