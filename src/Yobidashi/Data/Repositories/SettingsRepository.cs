using Microsoft.Data.Sqlite;

namespace Yobidashi.Data.Repositories;

public class SettingsRepository
{
    private readonly DatabaseManager _db;

    public SettingsRepository(DatabaseManager db)
    {
        _db = db;
    }

    public string? Get(string key)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        return cmd.ExecuteScalar() as string;
    }

    public void Set(string key, string value)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO settings (key, value) VALUES (@key, @value)
                            ON CONFLICT(key) DO UPDATE SET value = @value";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        var val = Get(key);
        return val != null ? val == "true" : defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        Set(key, value ? "true" : "false");
    }

    public Dictionary<string, string> GetAll()
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings";
        var dict = new Dictionary<string, string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dict[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
        }
        return dict;
    }
}
