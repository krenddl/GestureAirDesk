using System.Security.Cryptography;
using System.Text;

namespace GestureFlow.Desktop;

internal static class DataDirectory
{
    static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GestureFlow.AirDesk");

    public static string Pathname { get; } = ResolvePath();

    public static string InstanceSuffix { get; } = string.Equals(Pathname, DefaultPath, StringComparison.OrdinalIgnoreCase)
        ? ""
        : "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Pathname.ToUpperInvariant())))[..24];

    static string ResolvePath()
    {
        var configured = Environment.GetEnvironmentVariable("GESTUREFLOW_DATA_DIRECTORY");
        return string.IsNullOrWhiteSpace(configured)
            ? DefaultPath
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
    }

    public static string File(string name) => Path.Combine(Pathname, name);
}