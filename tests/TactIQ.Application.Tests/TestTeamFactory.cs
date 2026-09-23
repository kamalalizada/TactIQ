using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

internal static class TestTeamFactory
{
    public static Team Create(string name, int attributeLevel)
    {
        var team = new Team { Id = Guid.NewGuid(), Name = name };

        var layout = new[]
        {
            (Position.GK, 1), (Position.DEF, 4), (Position.MID, 4), (Position.FWD, 2)
        };

        foreach (var (position, count) in layout)
        {
            for (var i = 0; i < count; i++)
            {
                team.Players.Add(new Player
                {
                    Id = Guid.NewGuid(),
                    TeamId = team.Id,
                    Name = $"{name} {position}{i}",
                    Position = position,
                    IsStarter = true,
                    Overall = attributeLevel,
                    Pace = attributeLevel,
                    Shooting = attributeLevel,
                    Passing = attributeLevel,
                    Dribbling = attributeLevel,
                    Defending = attributeLevel,
                    Physical = attributeLevel
                });
            }
        }

        return team;
    }

    /// <summary>A full 19-man squad: eleven starters plus a bench that covers every position.</summary>
    public static Team CreateWithBench(string name, int attributeLevel, int benchLevel = 0)
    {
        var team = Create(name, attributeLevel);
        var level = benchLevel == 0 ? attributeLevel - 5 : benchLevel;

        var bench = new[]
        {
            (Position.GK, 1), (Position.DEF, 2), (Position.MID, 3), (Position.FWD, 2)
        };

        foreach (var (position, count) in bench)
        {
            for (var i = 0; i < count; i++)
            {
                team.Players.Add(new Player
                {
                    Id = Guid.NewGuid(),
                    TeamId = team.Id,
                    Name = $"{name} sub {position}{i}",
                    Position = position,
                    IsStarter = false,
                    Overall = level,
                    Pace = level,
                    Shooting = level,
                    Passing = level,
                    Dribbling = level,
                    Defending = level,
                    Physical = level
                });
            }
        }

        return team;
    }
}
