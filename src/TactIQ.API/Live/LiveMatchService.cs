using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Application.Live;
using TactIQ.Application.Seasons;
using TactIQ.Application.Simulation;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.API.Live;

public class LiveMatchService(
    IServiceScopeFactory scopeFactory,
    IMatchEngine matchEngine,
    IHubContext<MatchHub> hub,
    ILogger<LiveMatchService> logger) : ILiveMatchService
{
    private static readonly ConcurrentDictionary<Guid, LiveMatchSession> Sessions = new();
    private static readonly ConcurrentDictionary<Guid, CancellationTokenSource> Loops = new();

    /// <summary>Real milliseconds per simulated minute at 1x speed.</summary>
    private const int BaseTickIntervalMs = 1000;

    public async Task<LiveStateDto> StartAsync(string userId, Guid matchId, StartLiveRequest request, CancellationToken ct = default)
    {
        if (Sessions.TryGetValue(matchId, out var running))
            return running.Snapshot();

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var match = await LoadMatchAsync(db, matchId, ct);
        var season = await db.Seasons.FirstOrDefaultAsync(s => s.Id == match.Competition.SeasonId, ct)
                     ?? throw new KeyNotFoundException($"Season {match.Competition.SeasonId} was not found.");

        if (season.UserId != userId)
            throw new KeyNotFoundException($"Match {matchId} was not found.");

        if (season.UserTeamId is not { } userTeamId)
            throw new InvalidOperationException("Live matches are only available in Manager Mode.");

        if (match.HomeTeamId != userTeamId && match.AwayTeamId != userTeamId)
            throw new InvalidOperationException("Only your own team's match can be played live.");

        if (match.IsPlayed)
            throw new InvalidOperationException("This match has already been played.");

        var session = new LiveMatchSession(
            match, userTeamId, request.Style, request.Formation, request.SpeedMultiplier, matchEngine);

        if (!Sessions.TryAdd(matchId, session))
            return Sessions[matchId].Snapshot();

        StartLoop(session);
        return session.Snapshot();
    }

    public LiveStateDto? GetState(Guid matchId) =>
        Sessions.TryGetValue(matchId, out var session) ? session.Snapshot() : null;

    public Task<LiveStateDto> UpdateTacticsAsync(string userId, Guid matchId, TacticsUpdateRequest request, CancellationToken ct = default)
    {
        var session = RequireSession(matchId);

        if (request.Style is { } style)
            session.ApplyStyle(style);

        if (!string.IsNullOrWhiteSpace(request.Formation))
            session.ApplyFormation(request.Formation);

        if (request.Substitution is { } sub &&
            !session.TrySubstitute(sub.PlayerOutId, sub.PlayerInId, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var snapshot = session.Snapshot();
        _ = BroadcastAsync(matchId, "TacticsUpdated", snapshot);
        return Task.FromResult(snapshot);
    }

    public LiveStateDto SetPaused(string userId, Guid matchId, bool paused)
    {
        var session = RequireSession(matchId);

        if (paused)
            session.Pause();
        else
            session.Resume();

        var snapshot = session.Snapshot();
        _ = BroadcastAsync(matchId, "StatusChanged", snapshot);
        return snapshot;
    }

    public LiveStateDto SetSpeed(string userId, Guid matchId, int speedMultiplier)
    {
        var session = RequireSession(matchId);
        session.ApplySpeed(speedMultiplier);

        var snapshot = session.Snapshot();
        _ = BroadcastAsync(matchId, "StatusChanged", snapshot);
        return snapshot;
    }

    public async Task<LiveStateDto> FinishNowAsync(string userId, Guid matchId, CancellationToken ct = default)
    {
        var session = RequireSession(matchId);

        // Stop the ticking loop first so it cannot persist the match twice.
        if (Loops.TryRemove(matchId, out var loop))
        {
            await loop.CancelAsync();
            loop.Dispose();
        }

        var produced = session.FastForward();
        var snapshot = session.Snapshot();

        await PersistAsync(session, ct);
        Sessions.TryRemove(matchId, out _);

        foreach (var e in produced)
            await BroadcastAsync(matchId, "MatchEvent", e);

        await BroadcastAsync(matchId, "MatchFinished", snapshot);
        return snapshot;
    }

    private void StartLoop(LiveMatchSession session)
    {
        var cts = new CancellationTokenSource();
        Loops[session.MatchId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested && session.Status != LiveMatchStatus.Finished)
                {
                    // Re-read the speed each tick so 1x/2x/4x switches apply at once.
                    await Task.Delay(TimeSpan.FromMilliseconds(BaseTickIntervalMs / (double)session.SpeedMultiplier), cts.Token);

                    if (session.Status == LiveMatchStatus.Finished)
                        break;

                    if (session.Status == LiveMatchStatus.Paused)
                        continue;

                    var produced = session.Tick();
                    var snapshot = session.Snapshot();

                    await BroadcastAsync(session.MatchId, "Tick", snapshot);

                    foreach (var e in produced)
                        await BroadcastAsync(session.MatchId, "MatchEvent", e);
                }

                if (session.Status == LiveMatchStatus.Finished)
                {
                    await PersistAsync(session, cts.Token);
                    await BroadcastAsync(session.MatchId, "MatchFinished", session.Snapshot());
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown or an explicit stop; nothing to report.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Live match {MatchId} failed", session.MatchId);
            }
            finally
            {
                Sessions.TryRemove(session.MatchId, out _);
                Loops.TryRemove(session.MatchId, out _);
                cts.Dispose();
            }
        });
    }

    private async Task PersistAsync(LiveMatchSession session, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var match = await LoadMatchAsync(db, session.MatchId, ct);

        match.HomeGoals = session.HomeGoals;
        match.AwayGoals = session.AwayGoals;
        match.IsPlayed = true;

        foreach (var e in session.Events)
        {
            match.Events.Add(new MatchEvent
            {
                Minute = e.Minute,
                EventType = e.EventType,
                TeamId = e.TeamId,
                PlayerId = e.PlayerId,
                AssistPlayerId = e.AssistPlayerId,
                Description = e.Description
            });
        }

        SquadAftermath.Apply(
            match.HomeTeam.Players.Concat(match.AwayTeam.Players),
            session.Aftermath);

        // A match the manager played out himself earns what any other does.
        SeasonService.PayResult(match);

        match.Tactics.Add(new TeamTactics
        {
            TeamId = session.UserTeamId,
            Minute = session.Minute,
            Formation = session.UserFormation,
            Style = session.UserStyle
        });

        var competition = await db.Competitions.FirstAsync(c => c.Id == session.CompetitionId, ct);

        // A knockout round is advanced by the cup service once both legs are in,
        // so only week-by-week competitions move their own clock here.
        var runsByWeek = competition.Kind == CompetitionKind.League
                         || competition.CurrentRound == CupRound.GroupStage;

        if (runsByWeek)
        {
            var weekStillPending = await db.Matches
                .AnyAsync(m => m.CompetitionId == competition.Id
                            && m.WeekNumber == competition.CurrentWeek
                            && !m.IsPlayed
                            && m.Id != match.Id, ct);

            if (!weekStillPending && match.WeekNumber == competition.CurrentWeek)
                competition.CurrentWeek++;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<Match> LoadMatchAsync(IAppDbContext db, Guid matchId, CancellationToken ct) =>
        await db.Matches
            .Include(m => m.Competition)
            .Include(m => m.HomeTeam).ThenInclude(t => t.Players)
            .Include(m => m.AwayTeam).ThenInclude(t => t.Players)
            .Include(m => m.Events)
            .Include(m => m.Tactics)
            .FirstOrDefaultAsync(m => m.Id == matchId, ct)
        ?? throw new KeyNotFoundException($"Match {matchId} was not found.");

    private static LiveMatchSession RequireSession(Guid matchId) =>
        Sessions.TryGetValue(matchId, out var session)
            ? session
            : throw new KeyNotFoundException($"No live match is running for {matchId}.");

    private Task BroadcastAsync(Guid matchId, string method, object payload) =>
        hub.Clients.Group(MatchHub.GroupFor(matchId)).SendAsync(method, payload);
}
