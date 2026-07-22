using System.Text.Json;

namespace Puantaj.Core.Licensing;

public sealed class LicenseSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public byte[] SerializePayload(LicenseData license) =>
        JsonSerializer.SerializeToUtf8Bytes(license, Options);

    public string SerializeCode(SignedLicense signedLicense) =>
        Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(signedLicense, Options));

    public SignedLicense DeserializeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalized = string.Concat(code.Where(character => !char.IsWhiteSpace(character)));
        return JsonSerializer.Deserialize<SignedLicense>(Base64UrlDecode(normalized), Options)
               ?? throw new FormatException("Lisans içeriği okunamadı.");
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - base64.Length % 4) % 4);
        return Convert.FromBase64String(base64);
    }
}
