using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Seeding;

public class TeamFactory(Random? random = null) : ITeamFactory
{
    private readonly Random _random = random ?? Random.Shared;

    // Strength centre per club, so the league has genuine favourites and underdogs.
    private static readonly (string Name, int Strength)[] Clubs =
    [
        ("Real Madrid", 84), ("Manchester City", 83), ("Bayern Munich", 82), ("Barcelona", 81),
        ("Liverpool", 80), ("Paris Saint-Germain", 79), ("Inter Milan", 77), ("Arsenal", 77),
        ("Chelsea", 75), ("Atletico Madrid", 75), ("Juventus", 74), ("Borussia Dortmund", 74),
        ("AC Milan", 73), ("Manchester United", 73), ("Napoli", 72), ("Tottenham Hotspur", 71),
        ("Benfica", 70), ("Ajax", 69), ("Porto", 69), ("Sevilla", 68)
    ];

    private static readonly string[] FirstNames =
    [
        "Lucas", "Mateo", "Leon", "Rafael", "Diogo", "Andrea", "Youssef", "Thomas",
        "Nicolas", "Marco", "Kevin", "Ibrahim", "Sergio", "Milan", "Julian", "Aaron",
        "Victor", "Emre", "Pedro", "Felix", "Noah", "Adrien", "Stefan", "Joao"
    ];

    private static readonly string[] LastNames =
    [
        "Silva", "Moreno", "Bauer", "Rossi", "Dubois", "Novak", "Fernandes", "Keller",
        "Laurent", "Ricci", "Vidal", "Haaland", "Bergmann", "Costa", "Almeida", "Duarte",
        "Kovacic", "Lindqvist", "Marchetti", "Weber", "Salah", "Okafor", "Petrov", "Mendes"
    ];

    // 11 starters in a 4-4-2 shape, plus an 8-man bench with cover in every position.
    private static readonly (Position Position, int Count)[] StartingShape =
    [
        (Position.GK, 1), (Position.DEF, 4), (Position.MID, 4), (Position.FWD, 2)
    ];

    private static readonly (Position Position, int Count)[] BenchShape =
    [
        (Position.GK, 1), (Position.DEF, 2), (Position.MID, 3), (Position.FWD, 2)
    ];

    // Substitutes sit a little below the first eleven.
    private const int BenchPenalty = 5;

    public List<Team> CreateTeams(Guid leagueId, int teamCount)
    {
        if (teamCount > Clubs.Length)
            throw new ArgumentOutOfRangeException(nameof(teamCount), $"At most {Clubs.Length} teams are supported.");

        var clubs = Clubs.OrderBy(_ => _random.Next()).Take(teamCount).ToList();

        return clubs.Select(club =>
        {
            var team = new Team { Id = Guid.NewGuid(), LeagueId = leagueId, Name = club.Name };
            team.Players = CreateSquad(team.Id, club.Strength);
            return team;
        }).ToList();
    }

    private List<Player> CreateSquad(Guid teamId, int teamStrength)
    {
        var players = new List<Player>(SquadRules.SquadSize);
        var usedNames = new HashSet<string>();
        var shirtNumber = 1;

        foreach (var (shape, isStarter) in new[] { (StartingShape, true), (BenchShape, false) })
        {
            var strength = isStarter ? teamStrength : teamStrength - BenchPenalty;

            foreach (var (position, count) in shape)
            {
                for (var i = 0; i < count; i++)
                    players.Add(CreatePlayer(teamId, position, strength, shirtNumber++, isStarter, usedNames));
            }
        }

        return players;
    }

    private Player CreatePlayer(
        Guid teamId,
        Position position,
        int teamStrength,
        int shirtNumber,
        bool isStarter,
        HashSet<string> usedNames)
    {
        int Attr(int bias) => Math.Clamp(teamStrength + bias + _random.Next(-6, 7), 30, 95);

        // Position bias: each role is strong where it matters and weaker elsewhere.
        var (pace, shooting, passing, dribbling, defending, physical) = position switch
        {
            Position.GK => (-18, -30, -12, -20, 8, 4),
            Position.DEF => (-4, -16, -6, -10, 12, 8),
            Position.MID => (2, -2, 10, 6, 0, -2),
            _ => (10, 12, -2, 8, -20, 0)
        };

        return new Player
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            Name = UniqueName(usedNames),
            Position = position,
            ShirtNumber = shirtNumber,
            IsStarter = isStarter,
            Pace = Attr(pace),
            Shooting = Attr(shooting),
            Passing = Attr(passing),
            Dribbling = Attr(dribbling),
            Defending = Attr(defending),
            Physical = Attr(physical)
        };
    }

    private string UniqueName(HashSet<string> usedNames)
    {
        // A 19-man squad out of 24x24 combinations collides often enough to matter.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var name = $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}";
            if (usedNames.Add(name))
                return name;
        }

        var fallback = $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]} {usedNames.Count}";
        usedNames.Add(fallback);
        return fallback;
    }
}
