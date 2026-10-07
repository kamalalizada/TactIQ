using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Transfers;

/// <summary>
/// The business the rest of the league does while the window is open.
///
/// Without this the market would be a shop the manager visits alone: every
/// other squad would stay exactly as it was seeded, and a club that sold its
/// best player would simply be weaker for ever. Here the computer's clubs buy
/// from each other and bid for the manager's players, so the league shifts
/// underneath him and a window has consequences he did not choose.
/// </summary>
public class TransferMarketRunner(IAppDbContext db)
{
    /// <summary>Moves the computer's clubs will complete in one window week.</summary>
    private const int DealsPerWeek = 4;

    /// <summary>Bids sent to the manager in one window week.</summary>
    private const int ApproachesPerWeek = 2;

    /// <summary>
    /// A club only bids with money it can spare, so one signing does not leave
    /// it unable to do anything else all season.
    /// </summary>
    private const decimal ShareOfBudgetPerDeal = 0.6m;

    public async Task RunWeekAsync(Season season, int totalWeeks, Random random, CancellationToken ct)
    {
        if (!TransferWindowRules.IsOpen(season.CurrentWeek, totalWeeks))
        {
            await ExpireOpenOffersAsync(season, ct);
            return;
        }

        var clubs = await db.Teams
            .Include(t => t.Players)
            .Where(t => t.SeasonId == season.Id)
            .ToListAsync(ct);

        if (clubs.Count < 2)
            return;

        var window = TransferWindowRules.At(season.CurrentWeek, totalWeeks);

        DoBusinessBetweenClubs(season, clubs, window, random);
        await ApproachTheManagerAsync(season, clubs, random, ct);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The computer's clubs trade among themselves. Each deal is a club looking
    /// at the line it is weakest in and paying the asking price for someone who
    /// improves it.
    /// </summary>
    private void DoBusinessBetweenClubs(
        Season season, List<Team> clubs, TransferWindow window, Random random)
    {
        var ai = clubs.Where(c => c.Id != season.UserTeamId).ToList();

        for (var deal = 0; deal < DealsPerWeek && ai.Count >= 2; deal++)
        {
            var buyer = ai[random.Next(ai.Count)];

            if (AiTransferPolicy.WeakestLine(buyer) is not { } line)
                continue;

            var spend = buyer.Budget * ShareOfBudgetPerDeal;

            // The best player in that position the buyer can both improve on
            // and afford, from a club that can spare him.
            var target = ai
                .Where(seller => seller.Id != buyer.Id)
                .SelectMany(seller => seller.Players.Select(p => (Seller: seller, Player: p)))
                .Where(x => x.Player.Position == line
                            && AiTransferPolicy.Wants(x.Player, buyer)
                            && SquadLimits.CanSell(x.Seller, x.Player)
                            && AiTransferPolicy.AskingPrice(x.Player, x.Seller) <= spend)
                .OrderByDescending(x => x.Player.Overall)
                .FirstOrDefault();

            if (target.Player is null)
                continue;

            var fee = AiTransferPolicy.BidFor(target.Player, target.Seller, buyer, random);

            Move(season, target.Player, target.Seller, buyer, fee, window);
        }
    }

    /// <summary>
    /// Clubs come in for the manager's players. The bid is left open: it is his
    /// to answer, which is the whole point of it.
    /// </summary>
    private async Task ApproachTheManagerAsync(
        Season season, List<Team> clubs, Random random, CancellationToken ct)
    {
        if (season.UserTeamId is not { } userTeamId)
            return;

        var mine = clubs.FirstOrDefault(c => c.Id == userTeamId);
        if (mine is null)
            return;

        var alreadyWanted = await db.TransferOffers
            .Where(o => o.SeasonId == season.Id
                        && o.FromTeamId == userTeamId
                        && o.Status == OfferStatus.Pending)
            .Select(o => o.PlayerId)
            .ToListAsync(ct);

        var suitors = clubs
            .Where(c => c.Id != userTeamId && SquadLimits.CanBuy(c))
            .OrderBy(_ => random.Next())
            .ToList();

        var sent = 0;

        foreach (var suitor in suitors)
        {
            if (sent >= ApproachesPerWeek)
                break;

            var wanted = mine.Players
                .Where(p => !alreadyWanted.Contains(p.Id)
                            && SquadLimits.CanSell(mine, p)
                            && AiTransferPolicy.Wants(p, suitor)
                            && AiTransferPolicy.AskingPrice(p, mine) <= suitor.Budget)
                .OrderByDescending(p => p.Overall)
                .FirstOrDefault();

            if (wanted is null)
                continue;

            db.TransferOffers.Add(new TransferOffer
            {
                Id = Guid.NewGuid(),
                SeasonId = season.Id,
                PlayerId = wanted.Id,
                FromTeamId = mine.Id,
                ToTeamId = suitor.Id,
                Amount = AiTransferPolicy.BidFor(wanted, mine, suitor, random),
                Status = OfferStatus.Pending,
                WeekMade = season.CurrentWeek,
                CreatedAt = DateTime.UtcNow
            });

            alreadyWanted.Add(wanted.Id);
            sent++;
        }
    }

    /// <summary>Completes a move between two computer clubs.</summary>
    private void Move(
        Season season, Player player, Team seller, Team buyer, decimal fee, TransferWindow window)
    {
        seller.Budget += fee;
        buyer.Budget -= fee;

        seller.Players.Remove(player);
        buyer.Players.Add(player);

        player.TeamId = buyer.Id;
        player.LeaveLineup();

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
            Amount = fee,
            Week = season.CurrentWeek,
            Window = window,
            CompletedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Bids left unanswered when the window shuts lapse, so a manager does not
    /// come back in March to an inbox of offers nobody can act on.
    /// </summary>
    private async Task ExpireOpenOffersAsync(Season season, CancellationToken ct)
    {
        var open = await db.TransferOffers
            .Where(o => o.SeasonId == season.Id && o.Status == OfferStatus.Pending)
            .ToListAsync(ct);

        if (open.Count == 0)
            return;

        foreach (var offer in open)
        {
            offer.Status = OfferStatus.Expired;
            offer.ResponseReason = "Transfer pəncərəsi bağlandı.";
        }

        await db.SaveChangesAsync(ct);
    }
}
