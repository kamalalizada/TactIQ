using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TactIQ.Application.Seeding;

public record SquadPlayer(
    string Name,
    string Position,
    int ShirtNumber,
    bool IsStarter,
    int Overall,
    int Pace,
    int Shooting,
    int Passing,
    int Dribbling,
    int Defending,
    int Physical);

public record Squad(string Name, int Strength, IReadOnlyList<SquadPlayer> Players);

/// <summary>Real club squads, embedded so seeding needs no external files at runtime.</summary>
public static class SquadData
{
    private const string ResourceName = "TactIQ.Application.Seeding.Data.squads.json";

    private static readonly Lazy<IReadOnlyList<Squad>> Cache = new(Load);

    public static IReadOnlyList<Squad> All => Cache.Value;

    private static IReadOnlyList<Squad> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        return JsonSerializer.Deserialize<List<Squad>>(stream, options)
               ?? throw new InvalidOperationException("Squad data could not be read.");
    }
}
