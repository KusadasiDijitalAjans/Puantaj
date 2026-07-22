namespace Puantaj.Core.Data;

public sealed record Shift(string Code, TimeSpan? StartTime, TimeSpan? EndTime, bool IsWorkShift, int DisplayOrder);
