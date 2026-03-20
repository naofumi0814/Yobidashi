using Microsoft.Data.Sqlite;
using Yobidashi.Data.Models;

namespace Yobidashi.Data.Repositories;

public class CategoryRepository
{
    private readonly DatabaseManager _db;

    public CategoryRepository(DatabaseManager db)
    {
        _db = db;
    }

    public List<Category> GetAll()
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM categories ORDER BY sort_order, name";
        var list = new List<Category>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Category
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                SortOrder = (int)reader.GetInt64(reader.GetOrdinal("sort_order"))
            });
        }
        return list;
    }

    public long Insert(string name)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO categories (name) VALUES (@name);
                            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", name);
        return (long)(cmd.ExecuteScalar() ?? 0);
    }

    public void Delete(long id)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM categories WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdateOrder(long id, int order)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE categories SET sort_order = @order WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@order", order);
        cmd.ExecuteNonQuery();
    }
}
