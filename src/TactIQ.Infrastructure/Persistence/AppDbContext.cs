using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Domain.Entities;
using TactIQ.Infrastructure.Identity;

namespace TactIQ.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IAppDbContext
{
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();
    public DbSet<TeamTactics> TeamTactics => Set<TeamTactics>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<League>(e =>
        {
            e.Property(l => l.UserId).IsRequired().HasMaxLength(450);
            e.Property(l => l.SeasonName).IsRequired().HasMaxLength(120);
            e.HasIndex(l => l.UserId);

            e.HasMany(l => l.Teams)
                .WithOne(t => t.League)
                .HasForeignKey(t => t.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(l => l.Matches)
                .WithOne(m => m.League)
                .HasForeignKey(m => m.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);

            // Computed from TeamCount; never persisted.
            e.Ignore(l => l.TotalWeeks);
            e.Ignore(l => l.IsFinished);
        });

        builder.Entity<Team>(e =>
        {
            e.Property(t => t.Name).IsRequired().HasMaxLength(120);
            e.Property(t => t.LogoUrl).HasMaxLength(500);
            e.HasIndex(t => t.LeagueId);

            e.HasMany(t => t.Players)
                .WithOne(p => p.Team)
                .HasForeignKey(p => p.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            // Derived from the player list.
            e.Ignore(t => t.AttackRating);
            e.Ignore(t => t.DefenseRating);
            e.Ignore(t => t.TeamOverall);
        });

        builder.Entity<Player>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(120);
            e.HasIndex(p => p.TeamId);
            e.Ignore(p => p.Overall);
        });

        builder.Entity<Match>(e =>
        {
            e.HasIndex(m => new { m.LeagueId, m.WeekNumber });

            // Both FKs point at Team; restrict so deleting a league cascades only once.
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
