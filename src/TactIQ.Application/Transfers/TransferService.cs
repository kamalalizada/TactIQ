using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Transfers;

public class TransferService(IAppDbContext db) : ITransferService
{
    public async Task<TransferWindowDto> GetWindowAsync(
        string userId, Guid seasonId, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, tracked: false, ct);
        var totalWeeks = await LeagueLengthAsync(seasonId, ct);

        var budget = season.UserTeamId is { } teamId
            ? await db.Teams.Where(t => t.Id == teamId).Select(t => t.Budget).FirstOrDefaultAsync(ct)
            : 0m;

        return new TransferWindowDto(
            TransferWindowRules.At(season.CurrentWeek, totalWeeks),
            TransferWindowRules.IsOpen(season.CurrentWeek, totalWeeks),
            season.CurrentWeek,
            TransferWindowRules.ClosesAfter(season.CurrentWeek, totalWeeks),
            TransferWindowRules.OpensAt(season.CurrentWeek, totalWeeks),
            budget);
    }

    public async Task<IReadOnlyList<MarketPlayerDto>> SearchMarketAsync(
        string userId, Guid seasonId, MarketQuery query, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, tracked: false, ct);
        var club = await ManagedClubAsync(season, tracked: false, ct);

        var players = db.Players
            .AsNoTracking()
            .Include(p => p.Team)
            .Where(p => p.Team.SeasonId == seasonId && p.TeamId != club.Id);

        if (query.Position is { } position)
            players = players.Where(p => p.Position == position);

        if (query.MinOverall is { } minOverall)
            players = players.Where(p => p.Overall >= minOverall);

        if (query.MaxAge is { } maxAge)
            players = players.Where(p => p.Age <= maxAge);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Managers type a surname in whatever case comes to hand, so the
            // match ignores it. ToLower translates to SQL on every provider,
            // where ILIKE would tie this layer to PostgreSQL.
            var term = query.Search.Trim().ToLowerInvariant();
            players = players.Where(p => p.Name.ToLower().Contains(term));
        }

        // Price is worked out in memory, so the list is narrowed in the database
        // first and only then priced and filtered on what it costs.
        var take = Math.Clamp(query.Take, 1, 200);

        // Price is worked out from rating and age, so it cannot be filtered in
        // the database. Where a ceiling is set, the rating that ceiling can
        // afford is worked out first and the query capped there — otherwise the
        // best players in the game fill the page and every one of them is then
        // thrown away for being too dear, leaving a manager on a budget with an
        // empty market.
        // Price is worked out from rating and age, so it cannot be filtered in
        // the database. Where a ceiling is set, the best rating that ceiling
        // could possibly afford is worked out first and the query capped there.
        // Without it the page fills with the finest players in the game, every
        // one of them is then thrown away for being too dear, and a manager on
        // a budget is told the market is empty.
        //
        // Even capped, the search has to look at more than it returns: an older
        // player of a given rating is affordable where a younger one of the
        // same rating is not, and only the pricing knows which.
        var reach = query.MaxPrice is { } ceiling
            ? PlayerValuation.HighestRatingUnder(ceiling)
            : int.MaxValue;

        var scanned = query.MaxPrice is null ? take * 3 : Math.Max(take * 10, 400);

        // A player with a real market value is priced by it, so the ceiling
        // applies to him directly; only the rest need the rating estimate.
        var maxPrice = query.MaxPrice ?? decimal.MaxValue;

        var found = await players
            .Where(p => p.MarketValue > 0 ? p.MarketValue <= maxPrice : p.Overall <= reach)
            .OrderByDescending(p => p.Overall)
            .Take(scanned)
            .ToListAsync(ct);

        var bidOn = await PendingOfferPlayerIdsAsync(seasonId, club.Id, ct);
        var squads = await SellingSquadsAsync(seasonId, ct);

        return found
            .Select(p => ToMarketDto(p, club, bidOn.Contains(p.Id), squads))
            .Where(dto => query.MaxPrice is not { } maxPrice || dto.Value <= maxPrice)
            .OrderByDescending(dto => dto.Overall)
            .Take(take)
            .ToList();
    }

    public async Task<IReadOnlyList<TransferOfferDto>> GetOffersAsync(
        string userId, Guid seasonId, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, tracked: false, ct);
        var club = await ManagedClubAsync(season, tracked: false, ct);

        var offers = await db.TransferOffers
            .AsNoTracking()
            .Include(o => o.Player)
            .Where(o => o.SeasonId == seasonId && (o.FromTeamId == club.Id || o.ToTeamId == club.Id))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return await ToOfferDtosAsync(offers, club.Id, ct);
    }

    public async Task<IReadOnlyList<TransferDto>> GetHistoryAsync(
        string userId, Guid seasonId, CancellationToken ct = default)
    {
        await OwnedSeasonAsync(userId, seasonId, tracked: false, ct);

        return await db.Transfers
            .AsNoTracking()
            .Where(t => t.SeasonId == seasonId)
            .OrderByDescending(t => t.CompletedAt)
            .Select(t => new TransferDto(
                t.Id, t.PlayerId, t.PlayerName, t.FromTeamName, t.ToTeamName,
                t.Amount, t.Week, t.Window, t.CompletedAt))
            .ToListAsync(ct);
    }

    public async Task<TransferOfferDto> MakeOfferAsync(
        string userId, Guid seasonId, MakeOfferRequest request, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, tracked: true, ct);
        var buyer = await ManagedClubAsync(season, tracked: true, ct);

        await EnsureWindowOpenAsync(season, ct);

        if (request.Amount <= 0)
            throw new InvalidOperationException("Təklif sıfırdan böyük olmalıdır.");

        var player = await db.Players
            .Include(p => p.Team)
            .ThenInclude(t => t.Players)
            .FirstOrDefaultAsync(p => p.Id == request.PlayerId && p.Team.SeasonId == seasonId, ct)
            ?? throw new KeyNotFoundException("Oyunçu tapılmadı.");

        if (player.TeamId == buyer.Id)
            throw new InvalidOperationException("Bu oyunçu artıq sizin klubunuzdadır.");

        if (request.Amount > buyer.Budget)
            throw new InvalidOperationException("Büdcəniz bu təklif üçün kifayət etmir.");

        if (SquadLimits.WhyCannotBuy(buyer) is { } full)
            throw new InvalidOperationException(full);

        if (SquadLimits.WhyCannotSell(player.Team, player) is { } kept)
            throw new InvalidOperationException(kept);

        var alreadyBidding = await db.TransferOffers.AnyAsync(
            o => o.SeasonId == seasonId
                 && o.PlayerId == player.Id
                 && o.ToTeamId == buyer.Id
                 && o.Status == OfferStatus.Pending,
            ct);

        if (alreadyBidding)
            throw new InvalidOperationException("Bu oyunçu üçün artıq gözləyən təklifiniz var.");

        var offer = new TransferOffer
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            PlayerId = player.Id,
            FromTeamId = player.TeamId,
            ToTeamId = buyer.Id,
            Amount = request.Amount,
            Status = OfferStatus.Pending,
            WeekMade = season.CurrentWeek,
            CreatedAt = DateTime.UtcNow
        };

        db.TransferOffers.Add(offer);

        // The selling club answers at once. A manager who had to wait a week to
        // hear back could not plan the rest of the window around the answer.
        var verdict = AiTransferPolicy.ConsiderOffer(player, player.Team, request.Amount);

        if (verdict.Accepted)
        {
            await CompleteAsync(offer, player, season, ct);
        }
        else
        {
            offer.Status = OfferStatus.Rejected;
            offer.ResponseReason = verdict.Reason;
            await db.SaveChangesAsync(ct);
        }

        offer.Player = player;

        return (await ToOfferDtosAsync([offer], buyer.Id, ct))[0];
    }

    public async Task<TransferOfferDto> RespondToOfferAsync(
        string userId, Guid seasonId, Guid offerId, bool accept, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, tracked: true, ct);
        var club = await ManagedClubAsync(season, tracked: true, ct);

        var offer = await db.TransferOffers
            .Include(o => o.Player)
            .FirstOrDefaultAsync(o => o.Id == offerId && o.SeasonId == seasonId, ct)
            ?? throw new KeyNotFoundException("Təklif tapılmadı.");

        if (offer.FromTeamId != club.Id)
            throw new InvalidOperationException("Bu təklif sizin klubunuza aid deyil.");

        if (!offer.IsOpen)
            throw new InvalidOperationException("Bu təklif artıq cavablandırılıb.");

        if (!accept)
        {
            offer.Status = OfferStatus.Rejected;
            offer.ResponseReason = "Klub təklifi rədd etdi.";
            await db.SaveChangesAsync(ct);

            return (await ToOfferDtosAsync([offer], club.Id, ct))[0];
        }

        await EnsureWindowOpenAsync(season, ct);

        var player = await db.Players
            .Include(p => p.Team)
            .ThenInclude(t => t.Players)
            .FirstAsync(p => p.Id == offer.PlayerId, ct);

        if (SquadLimits.WhyCannotSell(player.Team, player) is { } kept)
            throw new InvalidOperationException(kept);

        await CompleteAsync(offer, player, season, ct);

        return (await ToOfferDtosAsync([offer], club.Id, ct))[0];
    }

    public async Task<TransferOfferDto> WithdrawOfferAsync(
        string userId, Guid seasonId, Guid offerId, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, tracked: true, ct);
        var club = await ManagedClubAsync(season, tracked: false, ct);

        var offer = await db.TransferOffers
            .Include(o => o.Player)
            .FirstOrDefaultAsync(o => o.Id == offerId && o.SeasonId == seasonId, ct)
            ?? throw new KeyNotFoundException("Təklif tapılmadı.");

        if (offer.ToTeamId != club.Id)
            throw new InvalidOperationException("Bu təklifi siz göndərməmisiniz.");

        if (!offer.IsOpen)
            throw new InvalidOperationException("Bu təklif artıq cavablandırılıb.");

        offer.Status = OfferStatus.Withdrawn;
        await db.SaveChangesAsync(ct);

        return (await ToOfferDtosAsync([offer], club.Id, ct))[0];
    }

    /// <summary>
    /// Moves the player, pays the fee and writes the move into the record.
    ///
    /// A player arriving at a new club has no place in its shape yet: he is a
    /// substitute until the manager picks him, which is why the shirt and the
    /// slot are cleared rather than carried across.
    /// </summary>
    private async Task CompleteAsync(
        TransferOffer offer, Player player, Season season, CancellationToken ct)
    {
        var seller = await db.Teams.FirstAsync(t => t.Id == offer.FromTeamId, ct);
        var buyer = await db.Teams.FirstAsync(t => t.Id == offer.ToTeamId, ct);

        seller.Budget += offer.Amount;
        buyer.Budget -= offer.Amount;

        player.TeamId = buyer.Id;
        player.LeaveLineup();

        var totalWeeks = await LeagueLengthAsync(season.Id, ct);

        db.Transfers.Add(new Transfer
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            PlayerId = player.Id,
            PlayerName = player.Name,
            FromTeamId = seller.Id,
            FromTeamName = seller.Name,
            ToTeamId = buyer.Id,
            ToTeamName = buyer.Name,
            Amount = offer.Amount,
            Week = season.CurrentWeek,
            Window = TransferWindowRules.At(season.CurrentWeek, totalWeeks),
            CompletedAt = DateTime.UtcNow
        });

        offer.Status = OfferStatus.Accepted;
        offer.ResponseReason = null;

        // Every other bid for this player is dead the moment he moves.
        var others = await db.TransferOffers
            .Where(o => o.SeasonId == season.Id
                        && o.PlayerId == player.Id
                        && o.Id != offer.Id
                        && o.Status == OfferStatus.Pending)
            .ToListAsync(ct);

        foreach (var other in others)
        {
            other.Status = OfferStatus.Rejected;
            other.ResponseReason = "Oyunçu başqa kluba keçdi.";
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureWindowOpenAsync(Season season, CancellationToken ct)
    {
        var totalWeeks = await LeagueLengthAsync(season.Id, ct);

        if (!TransferWindowRules.IsOpen(season.CurrentWeek, totalWeeks))
            throw new InvalidOperationException("Transfer pəncərəsi bağlıdır.");
    }

    // ---- shared

    /// <summary>
    /// How long the league season runs, which is what the window calendar is
    /// measured against. A save always has a domestic league; a cup does not
    /// have weeks in the same sense.
    /// </summary>
    private async Task<int> LeagueLengthAsync(Guid seasonId, CancellationToken ct)
    {
        var teamCount = await db.Competitions
            .AsNoTracking()
            .Where(c => c.SeasonId == seasonId && c.Kind == CompetitionKind.League)
            .Select(c => c.TeamCount)
            .FirstOrDefaultAsync(ct);

        return teamCount > 1 ? 2 * (teamCount - 1) : 0;
    }

    private static MarketPlayerDto ToMarketDto(
        Player player, Team buyer, bool hasPendingOffer, IReadOnlyDictionary<Guid, SquadShape> squads)
    {
        var value = PlayerValuation.Of(player);

        // A player can be looked at even when he cannot be bought, so the
        // reason travels with him rather than removing him from the list.
        var unavailable =
            (squads.TryGetValue(player.TeamId, out var seller)
                ? seller.WhyCannotSell(player.Position)
                : null)
            ?? SquadLimits.WhyCannotBuy(buyer)
            ?? (buyer.Budget < value ? "Büdcəniz bu oyunçu üçün kifayət etmir." : null);

        return new MarketPlayerDto(
            player.Id, player.Name, player.Position, player.Role, player.Age, player.Nationality,
            player.Overall, player.Pace, player.Shooting, player.Passing,
            player.Dribbling, player.Defending, player.Physical,
            player.TeamId, player.Team.Name, player.Team.LogoUrl,
            value, hasPendingOffer, unavailable);
    }

    /// <summary>
    /// How big each club's squad is, and how many it has in each line, so a
    /// whole market listing can be checked against the selling limits without
    /// loading every squad in the season.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, SquadShape>> SellingSquadsAsync(
        Guid seasonId, CancellationToken ct)
    {
        var counts = await db.Players
            .AsNoTracking()
            .Where(p => p.Team.SeasonId == seasonId)
            .GroupBy(p => new { p.TeamId, p.Position })
            .Select(g => new { g.Key.TeamId, g.Key.Position, Count = g.Count() })
            .ToListAsync(ct);

        return counts
            .GroupBy(c => c.TeamId)
            .ToDictionary(
                g => g.Key,
                g => new SquadShape(
                    g.Sum(c => c.Count),
                    g.ToDictionary(c => c.Position, c => c.Count)));
    }

    /// <summary>A club's squad reduced to the numbers the selling limits need.</summary>
    private readonly record struct SquadShape(int Size, IReadOnlyDictionary<Position, int> ByLine)
    {
        public string? WhyCannotSell(Position position)
        {
            if (Size <= SquadLimits.MinimumSquad)
                return $"Satan klubun heyətində {SquadLimits.MinimumSquad} oyunçudan az qala bilməz.";

            var left = ByLine.GetValueOrDefault(position) - 1;
            var floor = SquadLimits.FloorFor(position);

            return left < floor
                ? $"Satan klubda bu mövqedə ən azı {floor} oyunçu qalmalıdır."
                : null;
        }
    }

    private async Task<HashSet<Guid>> PendingOfferPlayerIdsAsync(
        Guid seasonId, Guid buyerId, CancellationToken ct)
    {
        var ids = await db.TransferOffers
            .AsNoTracking()
            .Where(o => o.SeasonId == seasonId
                        && o.ToTeamId == buyerId
                        && o.Status == OfferStatus.Pending)
            .Select(o => o.PlayerId)
            .ToListAsync(ct);

        return [.. ids];
    }

    /// <summary>Names the clubs on each offer in one pass, rather than per row.</summary>
    private async Task<IReadOnlyList<TransferOfferDto>> ToOfferDtosAsync(
        IReadOnlyCollection<TransferOffer> offers, Guid managedClubId, CancellationToken ct)
    {
        if (offers.Count == 0)
            return [];

        var clubIds = offers
            .SelectMany(o => new[] { o.FromTeamId, o.ToTeamId })
            .Distinct()
            .ToList();

        var names = await db.Teams
            .AsNoTracking()
            .Where(t => clubIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        return offers
            .Select(o => new TransferOfferDto(
                o.Id,
                o.PlayerId,
                o.Player.Name,
                o.Player.Position,
                o.Player.Overall,
                o.Player.Age,
                PlayerValuation.Of(o.Player),
                o.FromTeamId,
                names.GetValueOrDefault(o.FromTeamId, "—"),
                o.ToTeamId,
                names.GetValueOrDefault(o.ToTeamId, "—"),
                o.Amount,
                o.Status,
                o.WeekMade,
                o.ResponseReason,
                o.FromTeamId == managedClubId))
            .ToList();
    }

    private async Task<Season> OwnedSeasonAsync(
        string userId, Guid seasonId, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.Seasons : db.Seasons.AsNoTracking();

        return await query.FirstOrDefaultAsync(s => s.Id == seasonId && s.UserId == userId, ct)
               ?? throw new KeyNotFoundException($"Season {seasonId} was not found.");
    }

    /// <summary>
    /// The club the manager runs. Transfers are something a manager does, so
    /// there is nothing to do here in God Mode.
    /// </summary>
    private async Task<Team> ManagedClubAsync(Season season, bool tracked, CancellationToken ct)
    {
        if (season.UserTeamId is not { } teamId)
            throw new InvalidOperationException("Transfer üçün idarə etdiyiniz klub olmalıdır.");

        var query = tracked ? db.Teams : db.Teams.AsNoTracking();

        return await query
                   .Include(t => t.Players)
                   .FirstOrDefaultAsync(t => t.Id == teamId, ct)
               ?? throw new KeyNotFoundException("Idarə etdiyiniz klub tapılmadı.");
    }
}
