using TactIQ.Application.Seeding;
using TactIQ.Application.Simulation;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Live;

/// <summary>
/// In-memory state of a match being played out minute by minute. Only the
/// manager's own match runs this way; every other match is settled instantly.
/// </summary>
public class LiveMatchSession
{
    private readonly List<LiveEventDto> _events = [];
    private readonly Lock _gate = new();
    private readonly IMatchEngine _engine;
    private readonly MatchSide _home;
    private readonly MatchSide _away;

    public LiveMatchSession(
        Match match,
        Guid userTeamId,
        TacticalStyle style,
        string formation,
        int speedMultiplier,
        IMatchEngine engine)
    {
        MatchId = match.Id;
        CompetitionId = match.CompetitionId;
        SeasonId = match.Competition.SeasonId;
        UserTeamId = userTeamId;
        HomeTeam = match.HomeTeam;
        AwayTeam = match.AwayTeam;
        UserStyle = style;
        UserFormation = formation;
        SpeedMultiplier = Math.Clamp(speedMultiplier, 1, 4);

        _engine = engine;

        var userIsHome = HomeTeam.Id == userTeamId;
        _home = engine.CreateSide(HomeTeam, isHome: true, userIsHome ? style : TacticalStyle.Balanced);
        _away = engine.CreateSide(AwayTeam, isHome: false, userIsHome ? TacticalStyle.Balanced : style);
    }

    public Guid MatchId { get; }
    public Guid CompetitionId { get; }

    /// <summary>The save file this match belongs to; the squad lives here.</summary>
    public Guid SeasonId { get; }
    public Guid UserTeamId { get; }

    public Team HomeTeam { get; }
    public Team AwayTeam { get; }

    public int Minute { get; private set; }
    public int HomeGoals => _home.Goals;
    public int AwayGoals => _away.Goals;
    public LiveMatchStatus Status { get; private set; } = LiveMatchStatus.Running;

    public TacticalStyle UserStyle { get; private set; }
    public string UserFormation { get; private set; }
    public int SubstitutionsUsed { get; private set; }

    private static readonly TimeSpan AbandonedAfter = TimeSpan.FromMinutes(10);
    private DateTime _pausedAt;

    public int SpeedMultiplier { get; private set; }

    public bool UserIsHome => HomeTeam.Id == UserTeamId;
    public Team UserTeam => UserIsHome ? HomeTeam : AwayTeam;
    public Team OpponentTeam => UserIsHome ? AwayTeam : HomeTeam;

    private MatchSide UserSide => UserIsHome ? _home : _away;

    /// <summary>Condition, cards and injuries both sides finished with.</summary>
    public IReadOnlyList<PlayerAftermath> Aftermath =>
        _engine.Aftermath(_home).Concat(_engine.Aftermath(_away)).ToList();

    public IReadOnlyList<LiveEventDto> Events
    {
        get { lock (_gate) return _events.ToList(); }
    }

    public void ApplyStyle(TacticalStyle style)
    {
        lock (_gate)
        {
            UserStyle = style;
            UserSide.Style = style;
        }
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
    public IReadOnlyList<LiveEventDto> FastForward()
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
            produced.AddRange(Tick());

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

            var side = UserSide;
            var outgoing = side.Players.FirstOrDefault(p => p.Id == playerOutId);
            var incoming = side.Players.FirstOrDefault(p => p.Id == playerInId);

            if (outgoing is null || incoming is null)
            {
                error = "Both players must belong to your team.";
                return false;
            }

            if (!outgoing.OnPitch)
            {
                error = "The outgoing player is not on the pitch.";
                return false;
            }

            if (incoming.OnPitch)
            {
                error = "The incoming player is already on the pitch.";
                return false;
            }

            if (incoming.SentOff || incoming.Injured)
            {
                error = "That player cannot come on.";
                return false;
            }

            // Only the live side changes: the saved line-up is the manager's
            // standing selection and must survive a substitution made during a
            // single match.
            outgoing.OnPitch = false;
            incoming.OnPitch = true;

            SubstitutionsUsed++;

            _events.Add(new LiveEventDto(
                Minute,
                MatchEventType.Substitution,
                side.Team.Id,
                $"{incoming.Name} replaces {outgoing.Name}",
                incoming.Id));

            error = string.Empty;
            return true;
        }
    }

    /// <summary>Advances one simulated minute and returns whatever it produced.</summary>
    public IReadOnlyList<LiveEventDto> Tick()
    {
        lock (_gate)
        {
            if (Status != LiveMatchStatus.Running)
                return [];

            Minute++;

            var produced = _engine.PlayMinute(_home, _away, Minute)
                .Select(e => new LiveEventDto(e.Minute, e.EventType, e.TeamId, e.Description, e.PlayerId, e.AssistPlayerId))
                .ToList();

            _events.AddRange(produced);

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
            {
                Status = LiveMatchStatus.Paused;
                _pausedAt = DateTime.UtcNow;
            }
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

    /// <summary>
    /// A running match ends by itself, but a paused one would wait for ever,
    /// holding one of the few live slots after its manager has left.
    /// </summary>
    public bool IsAbandoned(DateTime now)
    {
        lock (_gate)
            return Status == LiveMatchStatus.Paused && now - _pausedAt >= AbandonedAfter;
    }

    public LiveStateDto Snapshot()
    {
        lock (_gate)
        {
            return new LiveStateDto(
                MatchId,
                SeasonId,
                CompetitionId,
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
                UserSide.Players
                    .Select(p => new LivePlayerDto(
                        p.Id,
                        p.Name,
                        p.Position,
                        p.NaturalRole,
                        p.PlayedRole,
                        p.ShirtNumber,
                        p.Overall,
                        (int)Math.Round(p.Stamina),
                        p.OnPitch,
                        p.SentOff,
                        p.Injured,
                        p.YellowCards))
                    .ToList(),
                _events.ToList());
        }
    }
}
