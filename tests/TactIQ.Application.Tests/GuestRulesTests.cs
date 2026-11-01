using TactIQ.Application.Guests;

namespace TactIQ.Application.Tests;

public class GuestRulesTests
{
    [Theory]
    [InlineData(0, 50, 0)]
    [InlineData(49, 50, 0)]
    // The newcomer is the fiftieth-first, so one must go.
    [InlineData(50, 50, 1)]
    // Lowering the cap brings the count back down in one step.
    [InlineData(53, 50, 4)]
    public void ANewcomerTakesThePlaceOfWhoeverIsOverTheCap(int existing, int maximum, int expected)
    {
        Assert.Equal(expected, GuestRules.HowManyToRemove(existing, maximum));
    }

    [Fact]
    public void AVisitorIsCountedByTheAddressTheProxiesReport()
    {
        Assert.Equal("203.0.113.9", GuestRules.ClientKey("203.0.113.9, 10.0.0.1", "10.0.0.2"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    public void WithoutAUsableHeaderTheConnectionItselfCounts(string? header)
    {
        Assert.Equal("127.0.0.1", GuestRules.ClientKey(header, "127.0.0.1"));
    }

    [Fact]
    public void WithNoAddressAtAllEveryoneSharesOneBucket()
    {
        Assert.Equal("unknown", GuestRules.ClientKey(null, null));
    }
}
