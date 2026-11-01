using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TactIQ.Application.Abstractions;
using TactIQ.Application.Careers;
using TactIQ.Application.Cup;
using TactIQ.Application.Seasons;
using TactIQ.Application.Transfers;
using TactIQ.Application.Scheduling;
using TactIQ.Application.Seeding;
using TactIQ.Application.Simulation;
using TactIQ.Application.Standings;
using TactIQ.Application.Statistics;
using TactIQ.Infrastructure.Identity;
using TactIQ.Infrastructure.Persistence;

namespace TactIQ.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTactIQInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IAuthService, AuthService>();

        services.Configure<GuestOptions>(configuration.GetSection(GuestOptions.SectionName));
        services.AddSingleton<GuestActivity>();
        services.AddScoped<IGuestService, GuestService>();

        services.AddSingleton<IScheduleGenerator, RoundRobinScheduleGenerator>();
        services.AddSingleton<IStandingsCalculator, StandingsCalculator>();
        services.AddSingleton<IStatisticsCalculator, StatisticsCalculator>();
        services.AddSingleton<IMatchEngine>(_ => new MatchEngine());
        services.AddSingleton<ITeamFactory>(_ => new TeamFactory());
        services.AddScoped<ISeasonService, SeasonService>();
        services.AddScoped<ICupService, CupService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<ICareerService, CareerService>();

        return services;
    }
}
