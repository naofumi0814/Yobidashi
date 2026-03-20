using Microsoft.Data.Sqlite;
using Yobidashi.Data.Models;

namespace Yobidashi.Data.Repositories;

public class ExcludedAppRepository
{
    private readonly DatabaseManager _db;

    public ExcludedAppRepository(DatabaseManager db)
    {
        _db = db;
    }

    public List<ExcludedApp> GetAll()
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM excluded_apps ORDER BY display_name, process_name";
        var list = new List<ExcludedApp>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ExcludedApp
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                ProcessName = reader.GetString(reader.GetOrdinal("process_name")),
                DisplayName = reader.IsDBNull(reader.GetOrdinal("display_name")) ? "" : reader.GetString(reader.GetOrdinal("display_name"))
            });
        }
        return list;
    }

    public HashSet<string> GetProcessNames()
    {
        return GetAll().Select(a => a.ProcessName.ToLowerInvariant()).ToHashSet();
    }

    public long Insert(string processName, string displayName)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO excluded_apps (process_name, display_name)
                            VALUES (@proc, @display);
                            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@proc", processName);
        cmd.Parameters.AddWithValue("@display", displayName);
        return (long)(cmd.ExecuteScalar() ?? 0);
    }

    public void Delete(long id)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM excluded_apps WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}
