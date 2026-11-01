using System.Net;

namespace TactIQ.Application.Guests;

/// <summary>
/// The decisions behind guest accounts that need no database to answer. Guests
/// are capped, so the free database cannot fill up, but the door never closes:
/// a newcomer takes the place of whoever has been away longest.
/// </summary>
public static class GuestRules
{
    /// <summary>How many existing guests must go to make room for one more.</summary>
    public static int HowManyToRemove(int existing, int maximum) =>
        Math.Max(0, existing - maximum + 1);

    /// <summary>
    /// Who a request is counted against for rate limiting. Behind the host's
    /// proxies the connection's own address is the proxy's, which would put every
    /// visitor in one bucket, so the first address in X-Forwarded-For is used.
    /// A client can forge that header; the cap on guests is what bounds the harm.
    /// </summary>
    public static string ClientKey(string? forwardedFor, string? remoteAddress)
    {
        var first = forwardedFor?.Split(',')[0].Trim();

        return IPAddress.TryParse(first, out var address)
            ? address.ToString()
            : remoteAddress ?? "unknown";
    }
}
