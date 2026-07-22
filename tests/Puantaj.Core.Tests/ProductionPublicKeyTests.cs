using System.Security.Cryptography;

namespace Puantaj.Core.Tests;

// PuantajApp bir WinForms/net8.0-windows projesi olduğundan buradan doğrudan referans alınamıyor
// (bu test projesi platformdan bağımsız kalmalı); bu yüzden gömülü açık anahtar kaynak metni
// üzerinden okunup doğrulanıyor. Amaç: PublicKeyProvider.Pem içindeki anahtarın gerçekten geçerli
// ve en az RSA-4096 gücünde bir açık anahtar olduğunu — geliştirme anahtarına (RSA-2048) dönülmediğini
// — kalıcı ve makine/gizli-anahtar bağımsız bir şekilde garanti altına almaktır.
public sealed class ProductionPublicKeyTests
{
    [Fact]
    public void EmbeddedPublicKeyIsAtLeastRsa4096()
    {
        var root = FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "PuantajApp", "PublicKeyProvider.cs"));
        var pem = ExtractPem(source);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        Assert.True(rsa.KeySize >= 4096, $"Gömülü açık anahtar {rsa.KeySize} bit; en az 4096 bit olmalı.");
        Assert.Contains("secrets/production.private.pem", source, StringComparison.Ordinal);
    }

    private static string ExtractPem(string source)
    {
        var start = source.IndexOf("Pem = \"\"\"", StringComparison.Ordinal);
        start = source.IndexOf('\n', start) + 1;
        var end = source.IndexOf("\"\"\"", start, StringComparison.Ordinal);
        var block = source[start..end];
        var lines = block.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);
        return string.Join('\n', lines);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Puantaj.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Proje kökü bulunamadı.");
    }
}
