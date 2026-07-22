namespace Puantaj.Core.Data;

public sealed record LockedMonth(int Year, int Month, DateTimeOffset LockedAt);
