using Microsoft.EntityFrameworkCore;
using TactIQ.Domain.Entities;

namespace TactIQ.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<League> Leagues { get; }
    DbSet<Team> Teams { get; }
    DbSet<Player> Players { get; }
    DbSet<Match> Matches { get; }
    DbSet<MatchEvent> MatchEvents { get; }
    DbSet<TeamTactics> TeamTactics { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
