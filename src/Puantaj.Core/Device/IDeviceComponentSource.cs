namespace Puantaj.Core.Device;

public interface IDeviceComponentSource
{
    IReadOnlyDictionary<string, string?> ReadComponents();
}
