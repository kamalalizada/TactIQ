namespace TactIQ.Application.Scheduling;

public interface IScheduleGenerator
{
    IReadOnlyList<ScheduledFixture> Generate(IReadOnlyList<Guid> teamIds);
}
