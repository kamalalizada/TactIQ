using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Application.Live;
using TactIQ.Application.Simulation;
using TactIQ.Domain.Entities;

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
        var league = await db.Leagues.FirstOrDefaultAsync(l => l.Id == match.LeagueId, ct)
                     ?? throw new KeyNotFoundException($"League {match.LeagueId} was not found.");

        if (league.UserId != userId)
            throw new KeyNotFoundException($"Match {matchId} was not found.");

        if (league.UserTeamId is not { } userTeamId)
            throw new InvalidOperationException("Live matches are only available in Manager Mode.");

        if (match.HomeTeamId != userTeamId && match.AwayTeamId != userTeamId)
            throw new InvalidOperationException("Only your own team's match can be played live.");

        if (match.IsPlayed)
            throw new InvalidOperationException("This match has already been played.");

        var session = new LiveMatchSession(match, userTeamId, request.Style, request.Formation, request.SpeedMultiplier);

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

        var produced = session.FastForward(matchEngine, new Random());
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
            var random = new Random();

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

                    var produced = session.Tick(matchEngine, random);
                    var snapshot = session.Snapshot();

                    await BroadcastAsync(session.MatchId, "Tick", snapshot);

                    if (produced is not null)
                        await BroadcastAsync(session.MatchId, "MatchEvent", produced);
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
                Description = e.Description
            });
        }

        match.Tactics.Add(new TeamTactics
        {
            TeamId = session.UserTeamId,
            Minute = session.Minute,
            Formation = session.UserFormation,
            Style = session.UserStyle
        });

        var league = await db.Leagues.FirstAsync(l => l.Id == session.LeagueId, ct);

        // The week was held open while this match was pending; it can advance now.
        var weekStillPending = await db.Matches
            .AnyAsync(m => m.LeagueId == league.Id && m.WeekNumber == league.CurrentWeek && !m.IsPlayed && m.Id != match.Id, ct);

        if (!weekStillPending && match.WeekNumber == league.CurrentWeek)
            league.CurrentWeek++;

        await db.SaveChangesAsync(ct);
    }

    private static async Task<Match> LoadMatchAsync(IAppDbContext db, Guid matchId, CancellationToken ct) =>
        await db.Matches
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
