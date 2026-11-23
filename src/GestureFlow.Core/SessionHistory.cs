using System.Text.Json;
namespace GestureFlow.Core;

public sealed record SessionSummary(DateTimeOffset Started, string Profile, int CameraIndex,
    double DurationSeconds, long DisplayedFrames, long GestureConfirmations, string Outcome)
{
    public double AverageFps => DurationSeconds > 0 ? DisplayedFrames / DurationSeconds : 0;
}
public sealed class SessionHistoryStore(string path)
{
    public IReadOnlyList<SessionSummary> Load()
    {
        if (!File.Exists(path)) return Array.Empty<SessionSummary>();
        if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException("История слишком велика.");
        var records = JsonSerializer.Deserialize<List<SessionSummary>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("История пуста.");
        if (records.Any(r => r == null || !double.IsFinite(r.DurationSeconds) || r.DurationSeconds < 0 ||
            r.DisplayedFrames < 0 || r.GestureConfirmations < 0 || r.CameraIndex is < 0 or > 5 ||
            string.IsNullOrWhiteSpace(r.Profile) || string.IsNullOrWhiteSpace(r.Outcome)))
            throw new InvalidDataException("Некорректные записи истории.");
        return records.OrderByDescending(r => r.Started).Take(30).ToArray();
    }
    public void Save(IEnumerable<SessionSummary> records)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(records.OrderByDescending(r => r.Started).Take(30),
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}

