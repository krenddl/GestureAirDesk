using System.Text.Json;
using GestureFlow.Core;
namespace GestureFlow.Desktop;

public sealed class SettingsStore {
    bool databaseLoadFailed;
    public string FilePath { get; } = DataDirectory.File("settings.json");

    public AppSettings Load()
    {
        if (PostgreSqlProfiles.Enabled) { try { PostgreSqlProfiles.Initialize(); var stored=PostgreSqlProfiles.LoadActive(); databaseLoadFailed=false; if(stored is not null)return stored; } catch { databaseLoadFailed=true; throw; } }
        if (!File.Exists(FilePath)) return new();
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))
            ?? throw new InvalidDataException("Файл настроек пуст.");
        if(!SettingsValidation.Valid(settings))throw new InvalidDataException("Некорректные значения настроек.");
        return settings;
    }

    public void Save(AppSettings settings)
    {
        if(!SettingsValidation.Valid(settings))throw new InvalidDataException("Некорректные настройки.");
        if(databaseLoadFailed)throw new InvalidOperationException("Настройки БД не загружены. Восстановите соединение и перезапустите приложение перед сохранением.");
        if(PostgreSqlProfiles.Enabled) PostgreSqlProfiles.SaveActive(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, overwrite: true);
    }
}







