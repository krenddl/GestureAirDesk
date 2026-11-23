using GestureFlow.Core;
using Npgsql;
namespace GestureFlow.Desktop;

public static class PostgreSqlProfiles
{
    static readonly string Config = DataDirectory.File("database.connection");
    public static bool Enabled => File.Exists(Config);
    internal static NpgsqlConnection Open()
    {
        var builder = new NpgsqlConnectionStringBuilder(File.ReadAllText(Config)) { Timeout = 3, CommandTimeout = 5, IncludeErrorDetail = false };
        var c = new NpgsqlConnection(builder.ConnectionString); c.Open(); return c;
    }
    public static void Initialize()
    {
        using var c = Open(); using var transaction = c.BeginTransaction(); using var cmd = new NpgsqlCommand("""
        CREATE TABLE IF NOT EXISTS profile_preferences (
          profile_id uuid PRIMARY KEY REFERENCES profile(profile_id) ON DELETE CASCADE,
          display_name varchar(60) NOT NULL,
          inset_x double precision NOT NULL CHECK(inset_x BETWEEN 0 AND 0.4),
          inset_y double precision NOT NULL CHECK(inset_y BETWEEN 0 AND 0.4),
          automation_gesture integer NOT NULL CONSTRAINT profile_preferences_gesture_range_v4 CHECK(automation_gesture BETWEEN 0 AND 6),
          automation_action integer NOT NULL CONSTRAINT profile_preferences_action_range_v6 CHECK(automation_action BETWEEN 0 AND 14),
          is_saved boolean NOT NULL
        );
        DO $migration$
        BEGIN
          LOCK TABLE profile_preferences IN ACCESS EXCLUSIVE MODE;
          IF EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'profile_preferences'::regclass
                     AND conname IN ('profile_preferences_automation_gesture_check', 'profile_preferences_gesture_range', 'profile_preferences_gesture_range_v3')) THEN
            ALTER TABLE profile_preferences
              DROP CONSTRAINT IF EXISTS profile_preferences_automation_gesture_check,
              DROP CONSTRAINT IF EXISTS profile_preferences_gesture_range,
              DROP CONSTRAINT IF EXISTS profile_preferences_gesture_range_v3,
              ADD CONSTRAINT profile_preferences_gesture_range_v4 CHECK(automation_gesture BETWEEN 0 AND 6);
          END IF;
          IF EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'profile_preferences'::regclass
                     AND conname IN ('profile_preferences_automation_action_check', 'profile_preferences_action_range', 'profile_preferences_action_range_v4', 'profile_preferences_action_range_v5')) THEN
            ALTER TABLE profile_preferences
              DROP CONSTRAINT IF EXISTS profile_preferences_automation_action_check,
              DROP CONSTRAINT IF EXISTS profile_preferences_action_range,
              DROP CONSTRAINT IF EXISTS profile_preferences_action_range_v4,
              DROP CONSTRAINT IF EXISTS profile_preferences_action_range_v5,
              ADD CONSTRAINT profile_preferences_action_range_v6 CHECK(automation_action BETWEEN 0 AND 14);
          END IF;
        END;
        $migration$;
        """, c, transaction);
        cmd.ExecuteNonQuery();
        transaction.Commit();
    }
    public static AppSettings? LoadActive() => Read(false).FirstOrDefault();
    public static IReadOnlyList<AppSettings> LoadProfiles() => Read(true);
    static List<AppSettings> Read(bool saved)
    {
        using var c=Open();using var cmd=new NpgsqlCommand("""
        SELECT p.camera_index,p.mirror,x.display_name,x.automation_gesture,x.automation_action,
               x.inset_x,x.inset_y,p.tracking_preset,p.click_kind,p.wheel_enabled,p.mouse_mode,p.auto_start_mouse
        FROM profile p JOIN profile_preferences x USING(profile_id) WHERE x.is_saved=@saved ORDER BY p.name
        """,c);cmd.Parameters.AddWithValue("saved",saved);using var r=cmd.ExecuteReader();var result=new List<AppSettings>();
        while(r.Read()) {
            var s=new AppSettings(r.GetInt16(0),r.GetBoolean(1),r.GetString(2),r.GetInt32(3),r.GetInt32(4),r.GetDouble(5),r.GetDouble(6),r.GetInt16(7),Array.IndexOf(new[]{"left","right","double","drag"},r.GetString(8)),r.GetBoolean(9),r.GetString(10)=="pinch"?1:0,r.GetBoolean(11));
            if(!SettingsValidation.Valid(s))throw new InvalidDataException("В базе обнаружены некорректные параметры профиля.");result.Add(s);
        }return result;
    }
    public static void SaveActive(AppSettings s){using var c=Open();using var t=c.BeginTransaction();Write(c,t,s,false);t.Commit();}
    public static void SaveProfiles(IReadOnlyList<AppSettings> profiles)
    {
        if(profiles.Count>20||profiles.Any(s=>!SettingsValidation.Valid(s))||profiles.Select(s=>s.ProfileName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=profiles.Count)throw new InvalidDataException("Некорректный список профилей.");
        using var c=Open();using var t=c.BeginTransaction();
        foreach(var s in profiles)Write(c,t,s,true);
        // Remove only managed named profiles, never reference data or session history.
        using var clear=new NpgsqlCommand("DELETE FROM profile_preferences WHERE is_saved AND NOT(display_name=ANY(@names))",c,t);
        clear.Parameters.AddWithValue("names",profiles.Select(s=>s.ProfileName).ToArray());clear.ExecuteNonQuery();t.Commit();
    }
    static void Write(NpgsqlConnection c,NpgsqlTransaction t,AppSettings s,bool saved)
    {
        if(!SettingsValidation.Valid(s))throw new InvalidDataException("Некорректные параметры.");
        using var cmd=new NpgsqlCommand("""
        INSERT INTO profile(name,mirror,mouse_mode,click_kind,camera_index,tracking_preset,wheel_enabled,auto_start_mouse)
        VALUES(@name,@mirror,@mode,@click,@camera,@preset,@wheel,@auto)
        ON CONFLICT(lower(btrim(name))) DO UPDATE SET mirror=EXCLUDED.mirror,mouse_mode=EXCLUDED.mouse_mode,
        click_kind=EXCLUDED.click_kind,camera_index=EXCLUDED.camera_index,tracking_preset=EXCLUDED.tracking_preset,
        wheel_enabled=EXCLUDED.wheel_enabled,auto_start_mouse=EXCLUDED.auto_start_mouse RETURNING profile_id
        """,c,t);
        cmd.Parameters.AddWithValue("name",saved?s.ProfileName:"__airdesk_active__");cmd.Parameters.AddWithValue("mirror",s.MirrorPreview);cmd.Parameters.AddWithValue("mode",s.ClickTrigger==1?"pinch":"dwell");cmd.Parameters.AddWithValue("click",new[]{"left","right","double","drag"}[s.ClickMode]);cmd.Parameters.AddWithValue("camera",s.CameraIndex);cmd.Parameters.AddWithValue("preset",s.TrackingPresetIndex);cmd.Parameters.AddWithValue("wheel",s.WheelEnabled);cmd.Parameters.AddWithValue("auto",s.AutoStartMouse);
        var id=(Guid)cmd.ExecuteScalar()!;
        using var pref=new NpgsqlCommand("""
        INSERT INTO profile_preferences VALUES(@id,@label,@x,@y,@gesture,@action,@saved)
        ON CONFLICT(profile_id) DO UPDATE SET display_name=EXCLUDED.display_name,inset_x=EXCLUDED.inset_x,
        inset_y=EXCLUDED.inset_y,automation_gesture=EXCLUDED.automation_gesture,automation_action=EXCLUDED.automation_action,is_saved=EXCLUDED.is_saved
        """,c,t);
        pref.Parameters.AddWithValue("id",id);pref.Parameters.AddWithValue("label",s.ProfileName);pref.Parameters.AddWithValue("x",s.PointerInsetX);pref.Parameters.AddWithValue("y",s.PointerInsetY);pref.Parameters.AddWithValue("gesture",s.AutomationGesture);pref.Parameters.AddWithValue("action",s.AutomationAction);pref.Parameters.AddWithValue("saved",saved);pref.ExecuteNonQuery();
    }
}
