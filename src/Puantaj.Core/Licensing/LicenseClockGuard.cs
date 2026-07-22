using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Puantaj.Core.Licensing;

public sealed class LicenseClockGuard
{
    private const string Pepper = "Puantaj.ClockGuard.v1";

    public bool ValidateAndRecord(string path, Guid licenseId, string deviceId, DateTimeOffset now)
    {
        try
        {
            if (File.Exists(path))
            {
                var record = JsonSerializer.Deserialize<ClockRecord>(File.ReadAllText(path));
                if (record is null || record.LicenseId != licenseId || !FixedEquals(record.Hash, Hash(record.LastSeenUtc, licenseId, deviceId))) return false;
                if (now.ToUniversalTime() < record.LastSeenUtc.AddHours(-12)) return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var utc = now.ToUniversalTime();
            File.WriteAllText(path, JsonSerializer.Serialize(new ClockRecord(licenseId, utc, Hash(utc, licenseId, deviceId))));
            return true;
        }
        catch { return false; }
    }

    private static string Hash(DateTimeOffset time, Guid licenseId, string deviceId) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes($"{time:O}|{licenseId:D}|{deviceId}|{Pepper}")));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private sealed record ClockRecord(Guid LicenseId, DateTimeOffset LastSeenUtc, string Hash);
}
