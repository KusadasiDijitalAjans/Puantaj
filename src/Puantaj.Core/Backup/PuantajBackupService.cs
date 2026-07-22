using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Puantaj.Core.Data;

namespace Puantaj.Core.Backup;

public sealed class PuantajBackupService
{
    private const string DatabaseEntryName = "data/puantaj.db";
    private const string ManifestEntryName = "manifest.json";

    public void Create(string databasePath, string destinationZip, string? logoPath = null)
    {
        if (!File.Exists(databasePath)) throw new FileNotFoundException("Veritabanı bulunamadı.", databasePath);
        var outputDirectory = Path.GetDirectoryName(destinationZip);
        if (!string.IsNullOrWhiteSpace(outputDirectory)) Directory.CreateDirectory(outputDirectory);
        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            var snapshot = Path.Combine(temporaryDirectory, "puantaj.db");
            CreateSqliteSnapshot(databasePath, snapshot);
            using var archive = ZipFile.Open(destinationZip, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(snapshot, DatabaseEntryName, CompressionLevel.Optimal);
            string? logoEntry = null;
            if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            {
                logoEntry = $"assets/logo{Path.GetExtension(logoPath).ToLowerInvariant()}";
                archive.CreateEntryFromFile(logoPath, logoEntry, CompressionLevel.Optimal);
            }
            var manifest = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(manifest.Open());
            writer.Write(JsonSerializer.Serialize(new BackupManifest(1, DateTimeOffset.UtcNow, logoEntry)));
        }
        finally { TryDeleteDirectory(temporaryDirectory); }
    }

    public void Restore(string sourceZip, string databasePath)
    {
        if (!File.Exists(sourceZip)) throw new FileNotFoundException("Yedek dosyası bulunamadı.", sourceZip);
        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            using var archive = ZipFile.OpenRead(sourceZip);
            var databaseEntry = archive.GetEntry(DatabaseEntryName)
                ?? throw new InvalidDataException("Yedek içinde puantaj veritabanı bulunamadı.");
            var restoredDatabase = Path.Combine(temporaryDirectory, "puantaj.db");
            databaseEntry.ExtractToFile(restoredDatabase);
            ValidateDatabase(restoredDatabase);
            var targetDirectory = Path.GetDirectoryName(databasePath)
                ?? throw new InvalidOperationException("Veritabanı klasörü belirlenemedi.");
            Directory.CreateDirectory(targetDirectory);
            SqliteConnection.ClearAllPools();
            File.Copy(restoredDatabase, databasePath, true);

            var manifestEntry = archive.GetEntry(ManifestEntryName);
            if (manifestEntry is not null)
            {
                using var reader = new StreamReader(manifestEntry.Open());
                var manifest = JsonSerializer.Deserialize<BackupManifest>(reader.ReadToEnd());
                var logoEntry = manifest?.LogoEntry is null ? null : archive.GetEntry(manifest.LogoEntry);
                if (logoEntry is not null)
                {
                    var assetsDirectory = Path.Combine(targetDirectory, "assets");
                    Directory.CreateDirectory(assetsDirectory);
                    var logoTarget = Path.Combine(assetsDirectory, Path.GetFileName(logoEntry.FullName));
                    logoEntry.ExtractToFile(logoTarget, true);
                    var database = new PuantajDatabase(databasePath);
                    database.Initialize();
                    database.SaveSettings(database.GetSettings() with { LogoPath = logoTarget });
                }
            }
        }
        finally { TryDeleteDirectory(temporaryDirectory); }
    }

    private static void CreateSqliteSnapshot(string sourcePath, string snapshotPath)
    {
        using var source = new SqliteConnection(CreateUnpooledConnectionString(sourcePath));
        using var destination = new SqliteConnection(CreateUnpooledConnectionString(snapshotPath));
        source.Open(); destination.Open(); source.BackupDatabase(destination);
    }

    private static void ValidateDatabase(string path)
    {
        using var connection = new SqliteConnection(CreateUnpooledConnectionString(path, SqliteOpenMode.ReadOnly));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Employees','Shifts','Assignments','AppSettings','LockedMonths');";
        if (Convert.ToInt32(command.ExecuteScalar()) != 5) throw new InvalidDataException("Yedek veritabanı eksik veya geçersiz.");
    }

    private static string CreateUnpooledConnectionString(string path, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString();

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"puantaj-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path); return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private sealed record BackupManifest(int Version, DateTimeOffset CreatedAt, string? LogoEntry);
}
