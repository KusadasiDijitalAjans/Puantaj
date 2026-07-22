namespace Puantaj.Core.Data;

public sealed record MissingAttendance(long EmployeeId, string EmployeeName, DateOnly WorkDate);

public sealed record MonthCompletion(IReadOnlySet<long> CompletedEmployeeIds, IReadOnlyList<MissingAttendance> Missing);
