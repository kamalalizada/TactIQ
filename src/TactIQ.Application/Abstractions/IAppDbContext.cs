using Microsoft.EntityFrameworkCore;
using TactIQ.Domain.Entities;

namespace TactIQ.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Season> Seasons { get; }
    DbSet<Competition> Competitions { get; }
    DbSet<CompetitionEntry> CompetitionEntries { get; }
    DbSet<Team> Teams { get; }
    DbSet<Player> Players { get; }
    DbSet<Match> Matches { get; }
    DbSet<MatchEvent> MatchEvents { get; }
    DbSet<TeamTactics> TeamTactics { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
