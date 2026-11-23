using GestureFlow.Core;
using Npgsql;

namespace GestureFlow.Desktop;

public static class PostgreSqlHistory
{
    public static IReadOnlyList<SessionSummary> Load(SessionHistoryStore local)
    {
        using var connection = PostgreSqlProfiles.Open();
        using var schema = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS session_details (
                session_id uuid PRIMARY KEY REFERENCES session(session_id) ON DELETE CASCADE,
                started_at_key timestamptz NOT NULL,
                profile_label varchar(60) NOT NULL,
                camera_index smallint NOT NULL CHECK (camera_index BETWEEN 0 AND 5),
                gesture_confirmations bigint NOT NULL CHECK (gesture_confirmations >= 0),
                outcome text NOT NULL,
                UNIQUE (started_at_key, profile_label, camera_index)
            )
            """, connection);
        schema.ExecuteNonQuery();
        using var command = new NpgsqlCommand("""
            SELECT s.started_at, d.profile_label, d.camera_index, s.duration_ms,
                   s.frames, d.gesture_confirmations, d.outcome
            FROM session_details d JOIN session s USING (session_id)
            ORDER BY s.started_at DESC LIMIT 30
            """, connection);
        var records = new List<SessionSummary>();
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                records.Add(new SessionSummary(reader.GetFieldValue<DateTimeOffset>(0),
                    reader.GetString(1), reader.GetInt16(2), reader.GetInt64(3) / 1000d,
                    reader.GetInt64(4), reader.GetInt64(5), reader.GetString(6)));
        if (records.Count > 0) return records;

        var legacy = local.Load();
        Save(legacy);
        return legacy;
    }

    public static void Save(IEnumerable<SessionSummary> records)
    {
        using var connection = PostgreSqlProfiles.Open();
        using var transaction = connection.BeginTransaction();
        // Serialize imports and writes so a retry cannot create a duplicate session.
        using var gate = new NpgsqlCommand("LOCK TABLE session_details IN SHARE ROW EXCLUSIVE MODE", connection, transaction);
        gate.ExecuteNonQuery();
        foreach (var record in records)
        {
            using var command = new NpgsqlCommand("""
                WITH new_session AS (
                    INSERT INTO session(profile_id, started_at, duration_ms, frames)
                    SELECT profile_id, @started, @duration, @frames
                    FROM profile
                    WHERE (lower(btrim(name)) = lower(btrim(@label)) OR name = '__airdesk_active__')
                    AND NOT EXISTS (
                        SELECT 1 FROM session_details
                        WHERE started_at_key = @started AND profile_label = @label AND camera_index = @camera
                    )
                    ORDER BY (name = '__airdesk_active__') LIMIT 1
                    RETURNING session_id
                )
                INSERT INTO session_details(session_id, started_at_key, profile_label, camera_index, gesture_confirmations, outcome)
                SELECT session_id, @started, @label, @camera, @gestures, @outcome FROM new_session
                """, connection, transaction);
            command.Parameters.AddWithValue("started", record.Started.ToUniversalTime());
            command.Parameters.AddWithValue("duration", checked((long)Math.Round(record.DurationSeconds * 1000)));
            command.Parameters.AddWithValue("frames", record.DisplayedFrames);
            command.Parameters.AddWithValue("label", record.Profile);
            command.Parameters.AddWithValue("camera", checked((short)record.CameraIndex));
            command.Parameters.AddWithValue("gestures", record.GestureConfirmations);
            command.Parameters.AddWithValue("outcome", record.Outcome);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
