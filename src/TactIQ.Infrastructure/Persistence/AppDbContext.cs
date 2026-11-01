using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Domain.Entities;
using TactIQ.Infrastructure.Identity;

namespace TactIQ.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IAppDbContext
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
    public DbSet<SeasonRecord> SeasonRecords => Set<SeasonRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // The cleanup sweep and the cap both look guests up by when they were last seen.
        builder.Entity<ApplicationUser>(e => e.HasIndex(u => new { u.IsGuest, u.LastActiveAt }));

        builder.Entity<Season>(e =>
        {
            e.Property(s => s.UserId).IsRequired().HasMaxLength(450);
            e.Property(s => s.Name).IsRequired().HasMaxLength(120);
            e.HasIndex(s => s.UserId);
            e.Ignore(s => s.IsFinished);

            e.HasMany(s => s.Teams)
                .WithOne(t => t.Season)
                .HasForeignKey(t => t.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(s => s.Competitions)
                .WithOne(c => c.Season)
                .HasForeignKey(c => c.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Competition>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(120);
            e.Property(c => c.Country).HasMaxLength(60);
            e.HasIndex(c => new { c.SeasonId, c.Kind });

            // Computed from the fixtures; never persisted.
            e.Ignore(c => c.TotalWeeks);
            e.Ignore(c => c.IsFinished);

            e.HasMany(c => c.Matches)
                .WithOne(m => m.Competition)
                .HasForeignKey(m => m.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(c => c.Entries)
                .WithOne(x => x.Competition)
                .HasForeignKey(x => x.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CompetitionEntry>(e =>
        {
            e.Property(x => x.GroupName).HasMaxLength(4);
            e.HasIndex(x => new { x.CompetitionId, x.TeamId }).IsUnique();

            // Deleting a season already removes its teams; a second cascade path
            // through the entry table is what PostgreSQL refuses.
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Team>(e =>
        {
            e.Property(t => t.Name).IsRequired().HasMaxLength(120);
            e.Property(t => t.Country).IsRequired().HasMaxLength(60);
            e.Property(t => t.LogoUrl).HasMaxLength(500);
            e.Property(t => t.Budget).HasPrecision(18, 2);
            e.HasIndex(t => new { t.SeasonId, t.Country });

            e.HasMany(t => t.Players)
                .WithOne(p => p.Team)
                .HasForeignKey(p => p.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            // Derived from the player list.
            e.Ignore(t => t.AttackRating);
            e.Ignore(t => t.DefenseRating);
            e.Ignore(t => t.TeamOverall);
        });

        builder.Entity<TransferOffer>(e =>
        {
            // Money is counted exactly: a fee printed as EUR 27.7M must not drift.
            e.Property(o => o.Amount).HasPrecision(18, 2);
            e.Property(o => o.ResponseReason).HasMaxLength(200);

            // Offers are read per season, and per club for the manager's inbox.
            e.HasIndex(o => new { o.SeasonId, o.Status });
            e.HasIndex(o => o.FromTeamId);
            e.HasIndex(o => o.ToTeamId);
            e.Ignore(o => o.IsOpen);

            e.HasOne(o => o.Season)
                .WithMany()
                .HasForeignKey(o => o.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            // A sold player keeps his offers; deleting the season clears them.
            e.HasOne(o => o.Player)
                .WithMany()
                .HasForeignKey(o => o.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Transfer>(e =>
        {
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.PlayerName).IsRequired().HasMaxLength(120);
            e.Property(t => t.FromTeamName).IsRequired().HasMaxLength(120);
            e.Property(t => t.ToTeamName).IsRequired().HasMaxLength(120);
            e.HasIndex(t => t.SeasonId);

            // Clubs and players are named here as they were at the time, so the
            // record survives whatever happens to them afterwards.
            e.HasOne(t => t.Season)
                .WithMany()
                .HasForeignKey(t => t.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SeasonRecord>(e =>
        {
            e.Property(r => r.CompetitionName).IsRequired().HasMaxLength(120);
            e.Property(r => r.Country).HasMaxLength(60);
            e.Property(r => r.ChampionTeamName).HasMaxLength(120);
            e.Property(r => r.UserBudget).HasPrecision(18, 2);

            // A career is read year by year, newest first.
            e.HasIndex(r => new { r.SeasonId, r.Year });

            e.HasOne(r => r.Season)
                .WithMany(s => s.History)
                .HasForeignKey(r => r.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Player>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(120);
            e.Property(p => p.Nationality).HasMaxLength(60);
            e.Property(p => p.MarketValue).HasPrecision(18, 2);
            e.HasIndex(p => p.TeamId);
            e.Ignore(p => p.IsAvailable);

            // Worked out from the keeping attributes; never stored.
            e.Ignore(p => p.Goalkeeping);
        });

        builder.Entity<Match>(e =>
        {
            e.HasIndex(m => new { m.CompetitionId, m.WeekNumber });
            e.HasIndex(m => m.TieId);
            e.Property(m => m.GroupName).HasMaxLength(4);
            e.Ignore(m => m.WentToPenalties);

            // Both FKs point at Team, so neither may cascade.
            e.HasOne(m => m.HomeTeam)
                .WithMany()
                .HasForeignKey(m => m.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.AwayTeam)
                .WithMany()
                .HasForeignKey(m => m.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(m => m.Events)
                .WithOne(ev => ev.Match)
                .HasForeignKey(ev => ev.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(m => m.Tactics)
                .WithOne(t => t.Match)
                .HasForeignKey(t => t.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MatchEvent>(e =>
        {
            e.Property(ev => ev.Description).HasMaxLength(300);
            e.HasIndex(ev => new { ev.MatchId, ev.Minute });
        });

        builder.Entity<TeamTactics>(e =>
        {
            e.Property(t => t.Formation).IsRequired().HasMaxLength(20);
            e.Property(t => t.PlayerChangesJson).HasColumnType("jsonb");
            e.HasIndex(t => new { t.MatchId, t.TeamId, t.Minute });
        });
    }
}
