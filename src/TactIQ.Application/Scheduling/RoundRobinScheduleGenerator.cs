namespace TactIQ.Application.Scheduling;

public class RoundRobinScheduleGenerator : IScheduleGenerator
{
    public IReadOnlyList<ScheduledFixture> Generate(IReadOnlyList<Guid> teamIds)
    {
        if (teamIds.Count < 2 || teamIds.Count % 2 != 0)
            throw new ArgumentException("Team count must be even and at least 2.", nameof(teamIds));

        var n = teamIds.Count;
        var roundsPerHalf = n - 1;
        var matchesPerRound = n / 2;

        // Rotating array: index 0 stays fixed, the rest rotate one step each round.
        var rotation = teamIds.ToArray();
        var fixtures = new List<ScheduledFixture>(roundsPerHalf * matchesPerRound * 2);

        for (var round = 0; round < roundsPerHalf; round++)
        {
            for (var i = 0; i < matchesPerRound; i++)
            {
                var a = rotation[i];
                var b = rotation[n - 1 - i];

                // Alternate home/away per round so no team is always at home.
                var (home, away) = (round + i) % 2 == 0 ? (a, b) : (b, a);

                fixtures.Add(new ScheduledFixture(round + 1, home, away));
                fixtures.Add(new ScheduledFixture(round + 1 + roundsPerHalf, away, home));
            }

            Rotate(rotation);
        }

        return fixtures.OrderBy(f => f.WeekNumber).ToList();
    }

    private static void Rotate(Guid[] teams)
    {
        var last = teams[^1];
        for (var i = teams.Length - 1; i > 1; i--)
            teams[i] = teams[i - 1];
        teams[1] = last;
    }
}
