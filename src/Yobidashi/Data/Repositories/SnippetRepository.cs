using Microsoft.Data.Sqlite;
using Yobidashi.Data.Models;

namespace Yobidashi.Data.Repositories;

/// <summary>
/// 定型文リポジトリ
/// </summary>
public class SnippetRepository
{
    private readonly DatabaseManager _db;

    public SnippetRepository(DatabaseManager db)
    {
        _db = db;
    }

    public List<Snippet> GetAll()
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM snippets ORDER BY updated_at DESC";
        return ReadSnippets(cmd);
    }

    public List<Snippet> GetEnabled()
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM snippets WHERE is_enabled = 1";
        return ReadSnippets(cmd);
    }

    public Snippet? GetById(long id)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM snippets WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return ReadSnippets(cmd).FirstOrDefault();
    }

    public Snippet? GetByTrigger(string triggerText)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM snippets WHERE trigger_text = @trigger AND is_enabled = 1";
        cmd.Parameters.AddWithValue("@trigger", triggerText);
        return ReadSnippets(cmd).FirstOrDefault();
    }

    public List<Snippet> Search(string query)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        // LIKE特殊文字をエスケープ
        var escaped = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var like = $"%{escaped}%";
        cmd.CommandText = @"SELECT * FROM snippets
            WHERE title LIKE @q ESCAPE '\' OR trigger_text LIKE @q ESCAPE '\' OR body LIKE @q ESCAPE '\' OR tags LIKE @q ESCAPE '\' OR memo LIKE @q ESCAPE '\'
            ORDER BY use_count DESC, updated_at DESC";
        cmd.Parameters.AddWithValue("@q", like);
        return ReadSnippets(cmd);
    }

    public List<Snippet> GetByCategory(string category)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM snippets WHERE category = @cat ORDER BY updated_at DESC";
        cmd.Parameters.AddWithValue("@cat", category);
        return ReadSnippets(cmd);
    }

    public long Insert(Snippet snippet)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO snippets
            (title, trigger_text, body, category, tags, memo, is_enabled, use_count, created_at, updated_at)
            VALUES (@title, @trigger, @body, @category, @tags, @memo, @enabled, 0, datetime('now','localtime'), datetime('now','localtime'));
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@title", snippet.Title);
        cmd.Parameters.AddWithValue("@trigger", snippet.TriggerText);
        cmd.Parameters.AddWithValue("@body", snippet.Body);
        cmd.Parameters.AddWithValue("@category", snippet.Category);
        cmd.Parameters.AddWithValue("@tags", snippet.Tags);
        cmd.Parameters.AddWithValue("@memo", snippet.Memo);
        cmd.Parameters.AddWithValue("@enabled", snippet.IsEnabled ? 1 : 0);
        return (long)(cmd.ExecuteScalar() ?? 0);
    }

    public void Update(Snippet snippet)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE snippets SET
            title = @title, trigger_text = @trigger, body = @body,
            category = @category, tags = @tags, memo = @memo,
            is_enabled = @enabled, updated_at = datetime('now','localtime')
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", snippet.Id);
        cmd.Parameters.AddWithValue("@title", snippet.Title);
        cmd.Parameters.AddWithValue("@trigger", snippet.TriggerText);
        cmd.Parameters.AddWithValue("@body", snippet.Body);
        cmd.Parameters.AddWithValue("@category", snippet.Category);
        cmd.Parameters.AddWithValue("@tags", snippet.Tags);
        cmd.Parameters.AddWithValue("@memo", snippet.Memo);
        cmd.Parameters.AddWithValue("@enabled", snippet.IsEnabled ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM snippets WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void IncrementUseCount(long id)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE snippets SET
            use_count = use_count + 1,
            last_used_at = datetime('now','localtime')
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void RecordExpansion(long snippetId, string appName)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO expansion_history (snippet_id, app_name) VALUES (@id, @app)";
        cmd.Parameters.AddWithValue("@id", snippetId);
        cmd.Parameters.AddWithValue("@app", appName);
        cmd.ExecuteNonQuery();

        IncrementUseCount(snippetId);
    }

    public List<Snippet> GetSorted(string sortBy)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sortBy switch
        {
            "use_count" => "SELECT * FROM snippets ORDER BY use_count DESC",
            "last_used" => "SELECT * FROM snippets ORDER BY CASE WHEN last_used_at IS NULL THEN 1 ELSE 0 END, last_used_at DESC",
            "title" => "SELECT * FROM snippets ORDER BY title",
            "trigger" => "SELECT * FROM snippets ORDER BY trigger_text",
            _ => "SELECT * FROM snippets ORDER BY updated_at DESC"
        };
        return ReadSnippets(cmd);
    }

    private List<Snippet> ReadSnippets(SqliteCommand cmd)
    {
        var list = new List<Snippet>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Snippet
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                Title = reader.GetString(reader.GetOrdinal("title")),
                TriggerText = reader.GetString(reader.GetOrdinal("trigger_text")),
                Body = reader.GetString(reader.GetOrdinal("body")),
                Category = reader.IsDBNull(reader.GetOrdinal("category")) ? "" : reader.GetString(reader.GetOrdinal("category")),
                Tags = reader.IsDBNull(reader.GetOrdinal("tags")) ? "[]" : reader.GetString(reader.GetOrdinal("tags")),
                Memo = reader.IsDBNull(reader.GetOrdinal("memo")) ? "" : reader.GetString(reader.GetOrdinal("memo")),
                IsEnabled = reader.GetInt64(reader.GetOrdinal("is_enabled")) == 1,
                UseCount = (int)reader.GetInt64(reader.GetOrdinal("use_count")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
                UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at"))),
                LastUsedAt = reader.IsDBNull(reader.GetOrdinal("last_used_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_used_at")))
            });
        }
        return list;
    }
}
