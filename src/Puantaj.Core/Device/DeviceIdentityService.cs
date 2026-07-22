using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Puantaj.Core.Device;

public sealed class DeviceIdentityService
{
    private readonly IDeviceComponentSource _source;

    public DeviceIdentityService(IDeviceComponentSource source) => _source = source;

    public string GetDeviceId()
    {
        var components = _source.ReadComponents()
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{Normalize(pair.Key)}={Normalize(pair.Value!)}")
            .ToArray();

        if (components.Length == 0)
        {
            throw new InvalidOperationException("Bu bilgisayardan cihaz kimliği üretmek için yeterli bilgi okunamadı.");
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", components)));
        var shortCode = Convert.ToHexString(digest.AsSpan(0, 8));
        return $"PUAN-{shortCode[..4]}-{shortCode[4..8]}-{shortCode[8..12]}-{shortCode[12..16]}";
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.Trim().ToUpperInvariant(), @"\s+", " ");
}
