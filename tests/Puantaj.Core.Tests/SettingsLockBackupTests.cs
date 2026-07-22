using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Puantaj.Core.Backup;
using Puantaj.Core.Data;

namespace Puantaj.Core.Tests;

public sealed class SettingsLockBackupTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"puantaj-g6-{Guid.NewGuid():N}");

    [Fact]
    public void Settings_AreCreatedLoadedAndSavedWithLogoPath()
    {
        var database = CreateDatabase();
        var initial = database.EnsureSettings("Otel A", "Mutfak");
        Assert.Equal("Otel A", initial.HotelName);

        var logoPath = Path.Combine(_directory, "logo.png");
        File.WriteAllBytes(logoPath, [1, 2, 3]);
        database.SaveSettings(initial with { HotelName = "Otel B", LogoPath = logoPath, DepartmentManager = "Ayşe", MarginLeftCm = 1.2m });

        var saved = database.GetSettings();
        Assert.Equal("Otel B", saved.HotelName);
        Assert.Equal(logoPath, saved.LogoPath);
        Assert.Equal("Ayşe", saved.DepartmentManager);
        Assert.Equal(1.2m, saved.MarginLeftCm);
    }

    [Fact]
    public void MonthCanBeLockedAndUnlocked()
    {
        var database = CreateDatabase();
        database.LockMonth(2026, 7);
        Assert.True(database.IsMonthLocked(2026, 7));
        Assert.Contains(database.GetLockedMonths(), item => item.Year == 2026 && item.Month == 7);
        database.UnlockMonth(2026, 7);
        Assert.False(database.IsMonthLocked(2026, 7));
    }

    [Fact]
    public void LockedMonthRejectsAssignmentWithoutChangingExistingData()
    {
        var database = CreateDatabase();
        var employee = database.AddEmployee("Ali");
        database.Assign(employee, new DateOnly(2026, 7, 1), "A");
        database.LockMonth(2026, 7);
        Assert.Throws<InvalidOperationException>(() => database.Assign(employee, new DateOnly(2026, 7, 1), "HT"));
        Assert.Equal("A", database.GetAssignments(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)).Single().Code);
    }

    [Fact]
    public void MonthLockRequiresEveryApplicableDayAndPersistsUntilExplicitlyUnlocked()
    {
        var database = CreateDatabase(); var employee = database.AddEmployee("Ali");
        database.UpdateEmployeeDetails(employee, "", "", new DateOnly(2026, 7, 10));
        for (var day = 10; day <= 31; day++) database.Assign(employee, new DateOnly(2026, 7, day), "A");
        database.ClearWeekAssignments(employee, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 20));
        var incomplete = database.LockMonthIfComplete(2026, 7);
        Assert.Equal(new DateOnly(2026, 7, 20), Assert.Single(incomplete.Missing).WorkDate);
        Assert.False(database.IsMonthLocked(2026, 7));
        database.Assign(employee, new DateOnly(2026, 7, 20), "A");
        var complete = database.LockMonthIfComplete(2026, 7);
        Assert.Empty(complete.Missing); Assert.Contains(employee, complete.CompletedEmployeeIds);
        Assert.True(new PuantajDatabase(database.DatabasePath).IsMonthLocked(2026, 7));
        database.UnlockMonth(2026, 7); Assert.False(database.IsMonthLocked(2026, 7));
    }

    [Fact]
    public void CompletionStopsBeforeEmploymentEndedDate()
    {
        var database = CreateDatabase(); var employee = database.AddEmployee("Ece");
        for (var day = 1; day <= 9; day++) database.Assign(employee, new DateOnly(2026, 7, day), "A");
        database.Assign(employee, new DateOnly(2026, 7, 10), "İA");
        var completion = database.EvaluateMonthCompletion(2026, 7);
        Assert.Empty(completion.Missing); Assert.Contains(employee, completion.CompletedEmployeeIds);
    }

    [Fact]
    public void BackupContainsDatabaseAndLogoButNotLicense()
    {
        var database = CreateDatabase();
        var settings = database.EnsureSettings("Otel", "Kat Hizmetleri");
        var logo = Path.Combine(_directory, "logo.png"); File.WriteAllBytes(logo, [5, 4, 3]);
        database.SaveSettings(settings with { LogoPath = logo });
        var backup = Path.Combine(_directory, "backup.zip");
        new PuantajBackupService().Create(database.DatabasePath, backup, logo);

        using var archive = ZipFile.OpenRead(backup);
        Assert.Contains(archive.Entries, item => item.FullName == "data/puantaj.db");
        Assert.Contains(archive.Entries, item => item.FullName.StartsWith("assets/logo", StringComparison.Ordinal));
        Assert.DoesNotContain(archive.Entries, item => item.FullName.Contains("license", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RestoreRecoversEmployeesAssignmentsShiftsSettingsAndLocks()
    {
        var database = CreateDatabase();
        var settings = database.EnsureSettings("Otel", "Mutfak");
        database.SaveSettings(settings with { GeneralManager = "Deniz" });
        database.SaveAssignmentCode("F", "Gece", new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), true);
        var employee = database.AddEmployee("Ece");
        database.Assign(employee, new DateOnly(2026, 6, 10), "F");
        database.LockMonth(2026, 6);
        var backup = Path.Combine(_directory, "backup.zip");
        new PuantajBackupService().Create(database.DatabasePath, backup);

        database.UnlockMonth(2026, 6); database.SetEmployeeActive(employee, false);
        new PuantajBackupService().Restore(backup, database.DatabasePath);

        var restored = new PuantajDatabase(database.DatabasePath); restored.Initialize();
        Assert.Equal("Deniz", restored.GetSettings().GeneralManager);
        Assert.Contains(restored.GetEmployees(), item => item.FullName == "Ece" && item.IsActive);
        Assert.Contains(restored.GetAssignmentCodes(), item => item.Code == "F" && item.Description == "Gece");
        Assert.Equal("F", restored.GetAssignments(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)).Single().Code);
        Assert.True(restored.IsMonthLocked(2026, 6));
    }

    private PuantajDatabase CreateDatabase()
    {
        Directory.CreateDirectory(_directory);
        var database = new PuantajDatabase(Path.Combine(_directory, $"{Guid.NewGuid():N}.db")); database.Initialize(); return database;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
