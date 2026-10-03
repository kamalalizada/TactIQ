using TactIQ.Domain.Enums;

namespace TactIQ.Application.Transfers;

/// <summary>Whether the market is open, and how long for.</summary>
public record TransferWindowDto(
    TransferWindow Window,
    bool IsOpen,
    int CurrentWeek,
    /// <summary>Last week of the window now open; null when it is shut.</summary>
    int? ClosesAfterWeek,
    /// <summary>Week the market next opens; null once winter has passed.</summary>
    int? OpensAtWeek,
    /// <summary>What the managed club has left to spend.</summary>
    decimal Budget);

/// <summary>A player as the market lists him: his club, his price, his ability.</summary>
public record MarketPlayerDto(
    Guid Id,
    string Name,
    Position Position,
    PlayerRole Role,
    int Age,
    string Nationality,
    int Overall,
    int Pace,
    int Shooting,
    int Passing,
    int Dribbling,
    int Defending,
    int Physical,
    Guid TeamId,
    string TeamName,
    string? TeamLogoUrl,
    decimal Value,
    /// <summary>True while this club already has a bid in for him.</summary>
    bool HasPendingOffer,
    /// <summary>Why he cannot be bought right now, or null if he can.</summary>
    string? Unavailable);

/// <summary>A bid, as either side of it sees it.</summary>
public record TransferOfferDto(
    Guid Id,
    Guid PlayerId,
    string PlayerName,
    Position PlayerPosition,
    int PlayerOverall,
    int PlayerAge,
    decimal PlayerValue,
    Guid FromTeamId,
    string FromTeamName,
    Guid ToTeamId,
    string ToTeamName,
    decimal Amount,
    OfferStatus Status,
    int WeekMade,
    string? ResponseReason,
    /// <summary>True when the managed club is the one being asked to sell.</summary>
    bool IsIncoming);

/// <summary>A move that went through.</summary>
public record TransferDto(
    Guid Id,
    Guid PlayerId,
    string PlayerName,
    string FromTeamName,
    string ToTeamName,
    decimal Amount,
    int Week,
    TransferWindow Window,
    DateTime CompletedAt);

public record MakeOfferRequest(Guid PlayerId, decimal Amount);

public record RespondToOfferRequest(bool Accept);

/// <summary>Filters for the market list; every one of them optional.</summary>
public record MarketQuery(
    Position? Position = null,
    int? MinOverall = null,
    int? MaxAge = null,
    decimal? MaxPrice = null,
    string? Search = null,
    int Take = 50);
