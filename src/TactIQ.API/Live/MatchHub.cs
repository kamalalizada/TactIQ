using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TactIQ.API.Live;

[Authorize]
public class MatchHub : Hub
{
    public static string GroupFor(Guid matchId) => $"match:{matchId}";

    public Task JoinMatch(Guid matchId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(matchId));

    public Task LeaveMatch(Guid matchId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(matchId));
}
