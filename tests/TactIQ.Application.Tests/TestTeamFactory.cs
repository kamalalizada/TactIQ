using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

internal static class TestTeamFactory
{
    public static Team Create(string name, int attributeLevel)
    {
        var team = new Team { Id = Guid.NewGuid(), Name = name, Formation = "4-4-2" };

        // The eleven stand in the slots a 4-4-2 asks for, each in their own
        // role, so nobody is quietly penalised for playing out of position.
        var layout = new[]
        {
            (Position.GK, PlayerRole.GK, 1),
            (Position.DEF, PlayerRole.LB, 1),
            (Position.DEF, PlayerRole.CB, 2),
            (Position.DEF, PlayerRole.RB, 1),
            (Position.MID, PlayerRole.LM, 1),
            (Position.MID, PlayerRole.CM, 2),
            (Position.MID, PlayerRole.RM, 1),
            (Position.FWD, PlayerRole.ST, 2)
        };

        var slot = 1;

        foreach (var (position, role, count) in layout)
        {
            for (var i = 0; i < count; i++)
            {
                team.Players.Add(new Player
                {
                    Id = Guid.NewGuid(),
                    TeamId = team.Id,
                    Name = $"{name} {role}{i}",
                    Position = position,
                    Role = role,
                    LineupSlot = slot++,
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

        // A bench that covers every job, so a substitution never forces
        // somebody into a role they cannot play.
        var bench = new[]
        {
            (Position.GK, PlayerRole.GK, 1),
            (Position.DEF, PlayerRole.CB, 1),
            (Position.DEF, PlayerRole.RB, 1),
            (Position.MID, PlayerRole.CM, 2),
            (Position.MID, PlayerRole.LM, 1),
            (Position.FWD, PlayerRole.ST, 2)
        };

        foreach (var (position, role, count) in bench)
        {
            for (var i = 0; i < count; i++)
            {
                team.Players.Add(new Player
                {
                    Id = Guid.NewGuid(),
                    TeamId = team.Id,
                    Name = $"{name} sub {role}{i}",
                    Position = position,
                    Role = role,
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
