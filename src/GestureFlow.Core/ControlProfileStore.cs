using System.Text.Json;
namespace GestureFlow.Core;
public static class SettingsValidation
{
    public static bool Valid(AppSettings s)=>s.CameraIndex is >=0 and <=5 &&
        !string.IsNullOrWhiteSpace(s.ProfileName)&&s.ProfileName.Length<=60&&
        s.AutomationGesture is >=0 and <=7 && s.AutomationAction is >=0 and <=15 &&
        double.IsFinite(s.PointerInsetX)&&double.IsFinite(s.PointerInsetY)&&
        s.PointerInsetX is >=0 and <=.4 && s.PointerInsetY is >=0 and <=.4 &&
        s.TrackingPresetIndex is >=0 and <=2 && s.ClickMode is >=0 and <=3 && s.ClickTrigger is >=0 and <=1;
}
public sealed class ControlProfileStore(string path)
{
    public Func<IReadOnlyList<AppSettings>>? DatabaseLoad { get; set; }
    public Action<IReadOnlyList<AppSettings>>? DatabaseSave { get; set; }
    public IReadOnlyList<AppSettings> Load(){
        if(DatabaseLoad is not null)return DatabaseLoad();
        if(!File.Exists(path))return [];
        if(new FileInfo(path).Length>1_000_000)throw new InvalidDataException("Файл профилей слишком велик.");
        var profiles=JsonSerializer.Deserialize<AppSettings[]>(File.ReadAllText(path))??throw new InvalidDataException("Пустой файл профилей.");
        Validate(profiles);return profiles;
    }
    static void Validate(IReadOnlyList<AppSettings> profiles){
        if(profiles.Count>20||profiles.Any(p=>p==null||!SettingsValidation.Valid(p))||
            profiles.Select(p=>p.ProfileName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=profiles.Count)
            throw new InvalidDataException("Некорректные профили или повторяющиеся имена.");
    }
    public void Save(IReadOnlyList<AppSettings> profiles){
        Validate(profiles);
        DatabaseSave?.Invoke(profiles);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(profiles,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(path+".tmp",path,true);
    }
}


