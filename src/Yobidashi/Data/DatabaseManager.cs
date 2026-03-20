using Microsoft.Data.Sqlite;

namespace Yobidashi.Data;

/// <summary>
/// SQLiteデータベース管理クラス
/// </summary>
public class DatabaseManager : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public static string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Yobidashi",
        "yobidashi.db");

    public DatabaseManager()
    {
        var dir = Path.GetDirectoryName(DatabasePath)!;
        Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={DatabasePath}";
    }

    public DatabaseManager(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={dbPath}";
    }

    public SqliteConnection GetConnection()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
            // WALモード有効化
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }
        return _connection;
    }

    public void Initialize()
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS snippets (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL DEFAULT '',
                trigger_text TEXT NOT NULL,
                body TEXT NOT NULL DEFAULT '',
                category TEXT DEFAULT '',
                tags TEXT DEFAULT '[]',
                memo TEXT DEFAULT '',
                is_enabled INTEGER DEFAULT 1,
                use_count INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime')),
                updated_at TEXT DEFAULT (datetime('now','localtime')),
                last_used_at TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_snippets_trigger ON snippets(trigger_text);
            CREATE INDEX IF NOT EXISTS idx_snippets_category ON snippets(category);
            CREATE INDEX IF NOT EXISTS idx_snippets_enabled ON snippets(is_enabled);

            CREATE TABLE IF NOT EXISTS categories (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                sort_order INTEGER DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS excluded_apps (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                process_name TEXT NOT NULL,
                display_name TEXT DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT
            );

            CREATE TABLE IF NOT EXISTS expansion_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                snippet_id INTEGER,
                expanded_at TEXT DEFAULT (datetime('now','localtime')),
                app_name TEXT DEFAULT '',
                FOREIGN KEY (snippet_id) REFERENCES snippets(id) ON DELETE CASCADE
            );
        ";
        cmd.ExecuteNonQuery();

        InsertDefaultData(conn);
    }

    private void InsertDefaultData(SqliteConnection conn)
    {
        // デフォルトカテゴリの挿入
        var defaultCategories = new[] { "ビジネス", "あいさつ", "メール", "SNS", "その他" };
        foreach (var cat in defaultCategories)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO categories (name, sort_order) VALUES (@name, @order)";
            cmd.Parameters.AddWithValue("@name", cat);
            cmd.Parameters.AddWithValue("@order", Array.IndexOf(defaultCategories, cat));
            cmd.ExecuteNonQuery();
        }

        // デフォルト設定
        var defaults = new Dictionary<string, string>
        {
            ["hotkey_search"] = "Ctrl+Space",
            ["trigger_key"] = "Space,Tab",
            ["auto_start"] = "false",
            ["is_paused"] = "false",
            ["theme"] = "system"
        };

        foreach (var (key, value) in defaults)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO settings (key, value) VALUES (@key, @value)";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.ExecuteNonQuery();
        }

        // サンプル定型文
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM snippets";
        var count = (long)(check.ExecuteScalar() ?? 0);
        if (count == 0)
        {
            InsertSampleSnippets(conn);
        }
    }

    private void InsertSampleSnippets(SqliteConnection conn)
    {
        var samples = new (string title, string trigger, string body, string category)[]
        {
            ("住所", "じゅうしょ",
             "〒100-0001\n東京都千代田区千代田1-1-1\nよびだしビル 3階",
             "ビジネス"),

            ("署名", "しょめい",
             "━━━━━━━━━━━━━━━━━━━━━━━━━\n山田 太郎（やまだ たろう）\n株式会社よびだし 開発部\nTEL: 03-1234-5678\nEmail: taro.yamada@example.com\n━━━━━━━━━━━━━━━━━━━━━━━━━",
             "ビジネス"),

            ("返信テンプレート", "へんしん",
             "いつもお世話になっております。\n株式会社よびだしの山田です。\n\nご連絡いただきありがとうございます。\n{{cursor}}\n\n引き続きよろしくお願いいたします。",
             "メール"),

            ("お礼文", "おれい",
             "このたびは誠にありがとうございます。\n心より感謝申し上げます。\n\n今後ともどうぞよろしくお願いいたします。",
             "あいさつ"),

            ("予約案内", "よやく",
             "ご予約ありがとうございます。\n\n■ 予約内容\n日時: {{date}} {{time}}\nお名前: {{cursor}}\n\nご不明な点がございましたら、お気軽にお問い合わせください。",
             "ビジネス"),

            ("お詫び文", "しゃざい",
             "このたびはご迷惑をおかけし、誠に申し訳ございません。\n深くお詫び申し上げます。\n\n{{cursor}}\n\n今後このようなことがないよう、再発防止に努めてまいります。\n何卒ご容赦くださいますようお願い申し上げます。",
             "ビジネス"),

            ("本日の日付", "きょう",
             "{{date}}",
             "その他"),

            ("現在時刻", "いま",
             "{{time}}",
             "その他"),
        };

        foreach (var (title, trigger, body, category) in samples)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO snippets (title, trigger_text, body, category, tags, memo)
                                VALUES (@title, @trigger, @body, @category, '[]', '')";
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@trigger", trigger);
            cmd.Parameters.AddWithValue("@body", body);
            cmd.Parameters.AddWithValue("@category", category);
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
