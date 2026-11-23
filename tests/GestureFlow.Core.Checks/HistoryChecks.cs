using GestureFlow.Core;
internal static class HistoryChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GestureFlow-check-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "history.json");
        try
        {
            var store = new SessionHistoryStore(path);
            if (store.Load().Count != 0) throw new Exception("История без файла.");
            var records = Enumerable.Range(0, 35).Select(i => new SessionSummary(
                DateTimeOffset.UnixEpoch.AddSeconds(i), "Тест", 0, 10, 100, 2, "Завершён")).ToArray();
            store.Save(records);
            var loaded = store.Load();
            if (loaded.Count != 30 || loaded[0].Started != records[34].Started || loaded[0].AverageFps != 10)
                throw new Exception("Сохранение, порядок, ограничение или FPS.");
            store.Save(loaded.Take(1));
            if (store.Load().Count != 1) throw new Exception("Замена истории.");
            File.WriteAllText(path, "broken");
            try { store.Load(); throw new Exception("Повреждение не обнаружено."); }
            catch (System.Text.Json.JsonException) { }
            store.Save([records[0] with { DurationSeconds = -1 }]);
            try { store.Load(); throw new Exception("Неверная длительность не отклонена."); }
            catch (InvalidDataException) { }
            Console.WriteLine("PASS: история — сохранение, замена, порядок, 30 записей, FPS, повреждение и валидация.");
        }
        finally { File.Delete(path); File.Delete(path + ".tmp"); Directory.Delete(directory); }
    }
}

