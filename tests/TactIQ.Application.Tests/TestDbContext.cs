using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Domain.Entities;

namespace TactIQ.Application.Tests;

/// <summary>
/// A database for the tests to work against.
///
/// It is the application's own contract over an in-memory store rather than
/// the real context, so these tests stay in the Application layer and do not
/// need PostgreSQL running to say whether a transfer is legal.
/// </summary>
internal sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<CompetitionEntry> CompetitionEntries => Set<CompetitionEntry>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();
    public DbSet<TeamTactics> TeamTactics => Set<TeamTactics>();
    public DbSet<TransferOffer> TransferOffers => Set<TransferOffer>();
    public DbSet<Transfer> Transfers => Set<Transfer>();

    public static TestDbContext Create() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"transfers-{Guid.NewGuid()}")
            .Options);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Computed properties the real context also keeps out of the store.
        builder.Entity<Season>().Ignore(s => s.IsFinished);
        builder.Entity<Competition>().Ignore(c => c.TotalWeeks).Ignore(c => c.IsFinished);
        builder.Entity<Team>()
            .Ignore(t => t.AttackRating)
            .Ignore(t => t.DefenseRating)
            .Ignore(t => t.TeamOverall);
        builder.Entity<Player>().Ignore(p => p.IsAvailable);
        builder.Entity<Match>().Ignore(m => m.WentToPenalties);
        builder.Entity<TransferOffer>().Ignore(o => o.IsOpen);
    }
}
