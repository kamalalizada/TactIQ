using System.Reflection;
using System.Text.Json;

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
    int Physical);

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
