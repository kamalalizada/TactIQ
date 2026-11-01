using System.Reflection;
using System.Text.Json;
using TactIQ.Application.Transfers;

namespace TactIQ.Application.Seeding;

public record SquadPlayer(
    string Name,
    string Position,
    string Role,
    int ShirtNumber,
    bool IsStarter,
    /// <summary>Where a starter stands in the club's shape; zero off the pitch.</summary>
    int LineupSlot,
    int Overall,
    int Age,
    string Nationality,
    int Pace,
    int Shooting,
    int Passing,
    int Dribbling,
    int Defending,
    int Physical,
    // Goalkeeping, for the keepers; zero for everybody else.
    int Diving = 0,
    int Handling = 0,
    int Kicking = 0,
    int Reflexes = 0,
    int Speed = 0,
    int Positioning = 0,
    /// <summary>The player's real market value in euros, where one is known.</summary>
    decimal? MarketValue = null);

public record Squad(
    string Name,
    string Country,
    string League,
    string? LogoUrl,
    int Strength,
    /// <summary>The shape this club lines up in, which its squad is built for.</summary>
    string Formation,
    IReadOnlyList<SquadPlayer> Players);

/// <summary>Real club squads, embedded so seeding needs no external files at runtime.</summary>
public static class SquadData
{
    private const string ResourceName = "TactIQ.Application.Seeding.Data.squads.json";

    private static readonly Lazy<IReadOnlyList<Squad>> Cache = new(Load);

    public static IReadOnlyList<Squad> All => Cache.Value;

    /// <summary>Clubs of one domestic league, strongest first.</summary>
    public static IReadOnlyList<Squad> ForCountry(string country) =>
        All.Where(s => string.Equals(s.Country, country, StringComparison.OrdinalIgnoreCase))
           .OrderByDescending(s => s.Strength)
           .ToList();

    /// <summary>
    /// Whether a club has a squad the game would let it keep: at least an eleven
    /// and a full set of changes, and no more than the market allows. A league's
    /// clubs can be listed before their players are, and such a club cannot play
    /// a match yet.
    /// </summary>
    public static bool IsPlayable(Squad squad) =>
        squad.Players.Count is >= SquadLimits.MinimumSquad and <= SquadLimits.MaximumSquad;

    /// <summary>Every club that can take the pitch.</summary>
    public static IReadOnlyList<Squad> Playable => All.Where(IsPlayable).ToList();

    /// <summary>Whether a league has clubs and every one of them is ready to play.</summary>
    public static bool IsLeagueReady(string country)
    {
        var squads = ForCountry(country);
        return squads.Count > 0 && squads.All(IsPlayable);
    }

    public static IReadOnlyList<string> Countries =>
        All.Select(s => s.Country).Distinct().OrderBy(c => c).ToList();

    private static IReadOnlyList<Squad> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        return JsonSerializer.Deserialize<List<Squad>>(stream, options)
               ?? throw new InvalidOperationException("Squad data could not be read.");
    }
}
