using TactIQ.Application.Seeding;
using TactIQ.Application.Simulation;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Live;

/// <summary>
/// In-memory state of a match being played out minute by minute. Only the
/// manager's own match runs this way; every other match is settled instantly.
/// </summary>
public class LiveMatchSession(Match match, Guid userTeamId, TacticalStyle style, string formation, int speedMultiplier)
{
    private readonly List<LiveEventDto> _events = [];
    private readonly Lock _gate = new();

    public Guid MatchId { get; } = match.Id;
    public Guid LeagueId { get; } = match.LeagueId;
    public Guid UserTeamId { get; } = userTeamId;

    public Team HomeTeam { get; } = match.HomeTeam;
    public Team AwayTeam { get; } = match.AwayTeam;

    public int Minute { get; private set; }
    public int HomeGoals { get; private set; }
    public int AwayGoals { get; private set; }
    public LiveMatchStatus Status { get; private set; } = LiveMatchStatus.Running;

    public TacticalStyle UserStyle { get; private set; } = style;
    public string UserFormation { get; private set; } = formation;
    public int SubstitutionsUsed { get; private set; }

    public int SpeedMultiplier { get; private set; } = Math.Clamp(speedMultiplier, 1, 4);

    public bool UserIsHome => HomeTeam.Id == UserTeamId;
    public Team UserTeam => UserIsHome ? HomeTeam : AwayTeam;
    public Team OpponentTeam => UserIsHome ? AwayTeam : HomeTeam;

    public IReadOnlyList<LiveEventDto> Events
    {
        get { lock (_gate) return _events.ToList(); }
    }

    public void ApplyStyle(TacticalStyle style)
    {
        lock (_gate) UserStyle = style;
    }

    public void ApplyFormation(string formation)
    {
        lock (_gate) UserFormation = formation;
    }

    public void ApplySpeed(int speedMultiplier)
    {
        lock (_gate) SpeedMultiplier = Math.Clamp(speedMultiplier, 1, 4);
    }

    /// <summary>
    /// Plays out every remaining minute at once, so the manager can skip to the
    /// final whistle. Returns the events those minutes produced.
    /// </summary>
    public IReadOnlyList<LiveEventDto> FastForward(IMatchEngine engine, Random random)
    {
        var produced = new List<LiveEventDto>();

        lock (_gate)
        {
            if (Status == LiveMatchStatus.Finished)
                return produced;

            // Resume first: a paused match would otherwise refuse to tick.
            Status = LiveMatchStatus.Running;
        }

        while (Status != LiveMatchStatus.Finished)
        {
            if (Tick(engine, random) is { } e)
                produced.Add(e);
        }

        return produced;
    }

    public bool TrySubstitute(Guid playerOutId, Guid playerInId, out string error)
    {
        lock (_gate)
        {
            if (SubstitutionsUsed >= SquadRules.MaxSubstitutions)
            {
                error = $"All {SquadRules.MaxSubstitutions} substitutions have been used.";
                return false;
            }

            var team = UserTeam;
            var outgoing = team.Players.FirstOrDefault(p => p.Id == playerOutId);
            var incoming = team.Players.FirstOrDefault(p => p.Id == playerInId);

            if (outgoing is null || incoming is null)
            {
                error = "Both players must belong to your team.";
                return false;
            }

            if (!outgoing.IsStarter)
            {
                error = "The outgoing player is not on the pitch.";
                return false;
            }

            if (incoming.IsStarter)
            {
                error = "The incoming player is already on the pitch.";
                return false;
            }

            outgoing.IsStarter = false;
            incoming.IsStarter = true;
            SubstitutionsUsed++;

            _events.Add(new LiveEventDto(
                Minute,
                MatchEventType.Substitution,
                team.Id,
                $"{incoming.Name} replaces {outgoing.Name}"));

            error = string.Empty;
            return true;
        }
    }

    /// <summary>Advances one simulated minute and returns any event it produced.</summary>
    public LiveEventDto? Tick(IMatchEngine engine, Random random)
    {
        lock (_gate)
        {
            if (Status != LiveMatchStatus.Running)
                return null;

            Minute++;

            var homeStyle = UserIsHome ? UserStyle : TacticalStyle.Balanced;
            var awayStyle = UserIsHome ? TacticalStyle.Balanced : UserStyle;

            var home = engine.ComputeStrength(HomeTeam, true, homeStyle, 0);
            var away = engine.ComputeStrength(AwayTeam, false, awayStyle, 0);

            LiveEventDto? produced = null;

            if (random.NextDouble() < engine.GoalChancePerMinute(home, away))
            {
                HomeGoals++;
                produced = new LiveEventDto(Minute, MatchEventType.Goal, HomeTeam.Id, $"Goal for {HomeTeam.Name}");
            }
            else if (random.NextDouble() < engine.GoalChancePerMinute(away, home))
            {
                AwayGoals++;
                produced = new LiveEventDto(Minute, MatchEventType.Goal, AwayTeam.Id, $"Goal for {AwayTeam.Name}");
            }

            if (produced is not null)
                _events.Add(produced);

            if (Minute >= EngineConstants.RegularTimeMinutes)
                Status = LiveMatchStatus.Finished;

            return produced;
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (Status == LiveMatchStatus.Running)
                Status = LiveMatchStatus.Paused;
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (Status == LiveMatchStatus.Paused)
                Status = LiveMatchStatus.Running;
        }
    }

    public LiveStateDto Snapshot()
    {
        lock (_gate)
        {
            return new LiveStateDto(
                MatchId,
                Minute,
                Status,
                HomeTeam.Id,
                HomeTeam.Name,
                HomeGoals,
                AwayTeam.Id,
                AwayTeam.Name,
                AwayGoals,
                UserStyle,
                UserFormation,
                SubstitutionsUsed,
                SquadRules.MaxSubstitutions - SubstitutionsUsed,
                SpeedMultiplier,
                _events.ToList());
        }
    }
}
