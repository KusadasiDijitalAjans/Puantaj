namespace Puantaj.Core.Data;

public sealed record Employee(long Id, string FullName, bool IsActive, int DisplayOrder, DateTimeOffset CreatedAt,
    string Position = "", string WorkPattern = "", DateOnly? HireDate = null)
{
    public bool IsEmployedOn(DateOnly date) => HireDate is null || date >= HireDate.Value;
}
