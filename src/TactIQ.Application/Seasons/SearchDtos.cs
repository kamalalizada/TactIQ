using TactIQ.Domain.Enums;

namespace TactIQ.Application.Seasons;

/// <summary>A club the search turned up.</summary>
public record TeamHitDto(
    Guid Id,
    string Name,
    string Country,
    string? LogoUrl,
    double TeamOverall);

/// <summary>A player the search turned up, with the club he plays for.</summary>
public record PlayerHitDto(
    Guid Id,
    string Name,
    Position Position,
    PlayerRole Role,
    int Age,
    int Overall,
    Guid TeamId,
    string TeamName,
    string? TeamLogoUrl,
    /// <summary>True when he plays for the club the manager runs.</summary>
    bool IsMine);

/// <summary>What one search found, clubs first.</summary>
public record SearchResultDto(
    IReadOnlyList<TeamHitDto> Teams,
    IReadOnlyList<PlayerHitDto> Players);
