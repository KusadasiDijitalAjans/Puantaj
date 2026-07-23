using Puantaj.Core.Data;

namespace Puantaj.Core.Planning;

public static class CopyCandidateEligibility
{
    public static bool HasMatchingActiveDates(
        Employee source,
        Employee target,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyList<AssignmentCodeDefinition> definitions)
    {
        if (to < from) throw new ArgumentOutOfRangeException(nameof(to));
        var resolver = new AssignmentCodeResolver(definitions);
        return ActiveDates(source).SequenceEqual(ActiveDates(target));

        IEnumerable<DateOnly> ActiveDates(Employee employee)
        {
            var employmentEnd = assignments
                .Where(item => item.EmployeeId == employee.Id && resolver.Resolve(item.Code).IsEmploymentEnded)
                .Select(item => (DateOnly?)item.WorkDate)
                .Min();
            for (var date = from; date <= to; date = date.AddDays(1))
                if (employee.IsEmployedOn(date) && (employmentEnd is null || date < employmentEnd))
                    yield return date;
        }
    }
}
