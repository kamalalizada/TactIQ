using TactIQ.Application.Live;
using TactIQ.Application.Seeding;
using TactIQ.Application.Simulation;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class LiveMatchSessionTests
{
    private static readonly MatchEngine Engine = new(new Random(7));

    private static LiveMatchSession CreateSession(
        out Team home,
        out Team away,
        TacticalStyle style = TacticalStyle.Balanced,
        bool userIsHome = true)
    {
        home = TestTeamFactory.CreateWithBench("Home", 75);
        away = TestTeamFactory.CreateWithBench("Away", 75);

        var match = new Match
        {
            Id = Guid.NewGuid(),
            LeagueId = Guid.NewGuid(),
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            HomeTeam = home,
            AwayTeam = away
        };

        var userTeamId = userIsHome ? home.Id : away.Id;
        return new LiveMatchSession(match, userTeamId, style, "4-4-2", 1, Engine);
    }

    [Fact]
    public void StartsAtMinuteZeroAndRunning()
    {
        var session = CreateSession(out _, out _);
        var state = session.Snapshot();

        Assert.Equal(0, state.Minute);
        Assert.Equal(LiveMatchStatus.Running, state.Status);
        Assert.Equal(0, state.HomeGoals + state.AwayGoals);
        Assert.Empty(state.Events);
    }

    [Fact]
    public void EachTickAdvancesExactlyOneMinute()
    {
        var session = CreateSession(out _, out _);

        session.Tick();
        session.Tick();

        Assert.Equal(2, session.Minute);
    }

    [Fact]
    public void MatchFinishesAfterNinetyMinutes()
    {
        var session = CreateSession(out _, out _);
        var random = new Random(42);

        for (var i = 0; i < EngineConstants.RegularTimeMinutes; i++)
            session.Tick();

        Assert.Equal(EngineConstants.RegularTimeMinutes, session.Minute);
        Assert.Equal(LiveMatchStatus.Finished, session.Status);
    }

    [Fact]
    public void TicksAfterFullTimeAreIgnored()
    {
        var session = CreateSession(out _, out _);
        var random = new Random(42);

        for (var i = 0; i < EngineConstants.RegularTimeMinutes + 10; i++)
            session.Tick();

        Assert.Equal(EngineConstants.RegularTimeMinutes, session.Minute);
    }

    [Fact]
    public void PausedSessionDoesNotAdvance()
    {
        var session = CreateSession(out _, out _);

        session.Pause();
        session.Tick();

        Assert.Equal(0, session.Minute);
        Assert.Equal(LiveMatchStatus.Paused, session.Status);

        session.Resume();
        session.Tick();

        Assert.Equal(1, session.Minute);
    }

    [Fact]
    public void GoalsAlwaysMatchTheRecordedGoalEvents()
    {
        var session = CreateSession(out var home, out var away);
        var random = new Random(2024);

        while (session.Status != LiveMatchStatus.Finished)
            session.Tick();

        var state = session.Snapshot();
        var goals = state.Events.Where(e => e.EventType == MatchEventType.Goal).ToList();

        Assert.Equal(state.HomeGoals, goals.Count(e => e.TeamId == home.Id));
        Assert.Equal(state.AwayGoals, goals.Count(e => e.TeamId == away.Id));
        Assert.All(goals, e => Assert.InRange(e.Minute, 1, EngineConstants.RegularTimeMinutes));
    }

    [Fact]
    public void StyleChangeTakesEffectImmediately()
    {
        var session = CreateSession(out _, out _);

        session.ApplyStyle(TacticalStyle.Attacking);

        Assert.Equal(TacticalStyle.Attacking, session.Snapshot().UserStyle);
    }

    [Fact]
    public void AttackingStyleScoresMoreThanDefensiveOverManyMatches()
    {
        var attackingGoals = 0;
        var defensiveGoals = 0;

        for (var seed = 0; seed < 60; seed++)
        {
            attackingGoals += PlayOut(TacticalStyle.Attacking, seed);
            defensiveGoals += PlayOut(TacticalStyle.Defensive, seed);
        }

        Assert.True(attackingGoals > defensiveGoals,
            $"attacking={attackingGoals}, defensive={defensiveGoals}");

        static int PlayOut(TacticalStyle style, int seed)
        {
            var home = TestTeamFactory.CreateWithBench("Home", 75);
            var away = TestTeamFactory.CreateWithBench("Away", 75);
            var match = new Match
            {
                Id = Guid.NewGuid(),
                HomeTeamId = home.Id,
                AwayTeamId = away.Id,
                HomeTeam = home,
                AwayTeam = away
            };

            var session = new LiveMatchSession(
                match, home.Id, style, "4-4-2", 1, new MatchEngine(new Random(seed)));

            while (session.Status != LiveMatchStatus.Finished)
                session.Tick();

            return session.HomeGoals;
        }
    }

    [Fact]
    public void SubstitutionSwapsStartersAndLogsAnEvent()
    {
        var session = CreateSession(out var home, out _);

        var outgoing = home.Players.First(p => p.IsStarter && p.Position == Position.MID);
        var incoming = home.Players.First(p => !p.IsStarter && p.Position == Position.MID);

        Assert.True(session.TrySubstitute(outgoing.Id, incoming.Id, out var error), error);

        Assert.False(outgoing.IsStarter);
        Assert.True(incoming.IsStarter);
        Assert.Equal(11, home.Players.Count(p => p.IsStarter));

        var state = session.Snapshot();
        Assert.Equal(1, state.SubstitutionsUsed);
        Assert.Contains(state.Events, e => e.EventType == MatchEventType.Substitution);
    }

    [Fact]
    public void SubstitutionIsRejectedOnceTheLimitIsReached()
    {
        var session = CreateSession(out var home, out _);

        var starters = home.Players.Where(p => p.IsStarter).Take(SquadRules.MaxSubstitutions + 1).ToList();
        var bench = home.Players.Where(p => !p.IsStarter).Take(SquadRules.MaxSubstitutions + 1).ToList();

        for (var i = 0; i < SquadRules.MaxSubstitutions; i++)
            Assert.True(session.TrySubstitute(starters[i].Id, bench[i].Id, out _));

        var lastIndex = SquadRules.MaxSubstitutions;
        Assert.False(session.TrySubstitute(starters[lastIndex].Id, bench[lastIndex].Id, out var error));
        Assert.Contains("substitutions", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubstitutionRejectsPlayerAlreadyOnThePitch()
    {
        var session = CreateSession(out var home, out _);

        var first = home.Players.First(p => p.IsStarter);
        var second = home.Players.Last(p => p.IsStarter);

        Assert.False(session.TrySubstitute(first.Id, second.Id, out var error));
        Assert.Contains("already on the pitch", error);
    }

    [Fact]
    public void SubstitutionRejectsAnotherTeamsPlayer()
    {
        var session = CreateSession(out var home, out var away);

        var outgoing = home.Players.First(p => p.IsStarter);
        var opponent = away.Players.First(p => !p.IsStarter);

        Assert.False(session.TrySubstitute(outgoing.Id, opponent.Id, out var error));
        Assert.Contains("your team", error);
    }

    [Fact]
    public void SubstitutingAStrongerPlayerRaisesTheTeamRating()
    {
        var session = CreateSession(out var home, out _);

        var weakest = home.Players.Where(p => p.IsStarter && p.Position == Position.FWD)
            .OrderBy(p => p.Overall).First();
        var bench = home.Players.First(p => !p.IsStarter && p.Position == Position.FWD);

        // Make the substitute clearly better so the rating has to move.
        bench.Shooting = 95;
        bench.Pace = 95;
        bench.Dribbling = 95;

        var before = home.AttackRating;
        Assert.True(session.TrySubstitute(weakest.Id, bench.Id, out _));

        Assert.True(home.AttackRating > before);
    }

    [Fact]
    public void FastForwardCompletesTheMatchInOneCall()
    {
        var session = CreateSession(out _, out _);

        session.Tick();
        session.FastForward();

        Assert.Equal(LiveMatchStatus.Finished, session.Status);
        Assert.Equal(EngineConstants.RegularTimeMinutes, session.Minute);
    }

    [Fact]
    public void FastForwardResumesAPausedMatch()
    {
        var session = CreateSession(out _, out _);

        session.Pause();
        session.FastForward();

        Assert.Equal(LiveMatchStatus.Finished, session.Status);
    }

    [Fact]
    public void AwayManagerControlsTheAwaySide()
    {
        var session = CreateSession(out _, out var away, TacticalStyle.Attacking, userIsHome: false);

        Assert.False(session.UserIsHome);
        Assert.Equal(away.Id, session.UserTeam.Id);
    }

    [Fact]
    public void SpeedIsClampedToSupportedRange()
    {
        var session = CreateSession(out _, out _);

        session.ApplySpeed(99);
        Assert.Equal(4, session.SpeedMultiplier);

        session.ApplySpeed(0);
        Assert.Equal(1, session.SpeedMultiplier);
    }
}
