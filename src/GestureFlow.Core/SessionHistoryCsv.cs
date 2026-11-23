using System.Globalization;
using System.Text;

namespace GestureFlow.Core;

public static class SessionHistoryCsv
{
    static readonly CultureInfo SpreadsheetCulture = CultureInfo.GetCultureInfo("ru-RU");

    public static string Format(IEnumerable<SessionSummary> sessions)
    {
        var csv = new StringBuilder("Начало;Профиль;Камера;Длительность (с);Кадры;Средний FPS;Жесты;Результат\r\n");
        foreach (var session in sessions)
        {
            csv.AppendJoin(';',
                session.Started.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                TextCell(session.Profile),
                session.CameraIndex.ToString(CultureInfo.InvariantCulture),
                session.DurationSeconds.ToString("0.###", SpreadsheetCulture),
                session.DisplayedFrames.ToString(CultureInfo.InvariantCulture),
                session.AverageFps.ToString("0.##", SpreadsheetCulture),
                session.GestureConfirmations.ToString(CultureInfo.InvariantCulture),
                TextCell(session.Outcome));
            csv.Append("\r\n");
        }
        return csv.ToString();
    }

    static string TextCell(string value)
    {
        // Prevent spreadsheet formula interpretation of user-defined profile names.
        if (value.Length > 0 && (char.IsControl(value[0]) ||
            value.TrimStart().StartsWithAny('=', '+', '-', '@')))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    static bool StartsWithAny(this string value, char first, char second, char third, char fourth) =>
        value.Length > 0 && (value[0] == first || value[0] == second || value[0] == third || value[0] == fourth);
}