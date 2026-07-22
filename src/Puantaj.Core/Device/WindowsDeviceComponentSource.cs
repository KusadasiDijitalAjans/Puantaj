using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace Puantaj.Core.Device;

[SupportedOSPlatform("windows")]
public sealed class WindowsDeviceComponentSource : IDeviceComponentSource
{
    public IReadOnlyDictionary<string, string?> ReadComponents()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new Dictionary<string, string?>();
        }

        return new Dictionary<string, string?>
        {
            ["MachineGuid"] = TryReadRegistry(
                RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Cryptography",
                "MachineGuid",
                RegistryView.Registry64),
            ["SystemVolumeSerial"] = TryReadSystemVolumeSerial(),
            ["BiosUuid"] = TryReadCommand("wmic", "csproduct get uuid /value", "UUID="),
            ["ProcessorId"] = TryReadCommand("wmic", "cpu get ProcessorId /value", "ProcessorId=")
                ?? TryReadRegistry(
                    RegistryHive.LocalMachine,
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                    "ProcessorNameString",
                    RegistryView.Default)
        };
    }

    private static string? TryReadRegistry(
        RegistryHive hive,
        string path,
        string name,
        RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path);
            return key?.GetValue(name)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadSystemVolumeSerial()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            return GetVolumeInformation(
                root,
                null,
                0,
                out var serial,
                out _,
                out _,
                null,
                0)
                ? serial.ToString("X8")
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadCommand(string fileName, string arguments, string prefix)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null || !process.WaitForExit(2_000) || process.ExitCode != 0)
            {
                return null;
            }

            var line = process.StandardOutput.ReadToEnd()
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return line is null ? null : line[prefix.Length..].Trim();
        }
        catch
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder? fileSystemNameBuffer,
        int fileSystemNameSize);
}
