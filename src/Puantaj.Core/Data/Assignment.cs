namespace Puantaj.Core.Data;

public sealed record Assignment(long Id, long EmployeeId, DateOnly WorkDate, string Code, DateTimeOffset UpdatedAt);
