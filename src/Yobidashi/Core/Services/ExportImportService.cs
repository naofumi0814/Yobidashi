using System.Text.Json;
using System.Text.Json.Serialization;
using Yobidashi.Data;
using Yobidashi.Data.Models;
using Yobidashi.Data.Repositories;

namespace Yobidashi.Core.Services;

/// <summary>
/// JSON エクスポート/インポート サービス
/// </summary>
public class ExportImportService
{
    private readonly SnippetRepository _snippetRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly DatabaseManager _databaseManager;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ExportImportService(
        SnippetRepository snippetRepository,
        CategoryRepository categoryRepository,
        DatabaseManager databaseManager)
    {
        _snippetRepository = snippetRepository;
        _categoryRepository = categoryRepository;
        _databaseManager = databaseManager;
    }

    /// <summary>全データをJSONファイルにエクスポート</summary>
    public async Task ExportAsync(string filePath)
    {
        var data = new ExportData
        {
            Version = "1.0",
            ExportedAt = DateTime.Now,
            Snippets = _snippetRepository.GetAll(),
            Categories = _categoryRepository.GetAll()
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);
    }

    /// <summary>JSONファイルからインポート</summary>
    public async Task<int> ImportAsync(string filePath, bool overwrite = false)
    {
        var json = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
        var data = JsonSerializer.Deserialize<ExportData>(json, JsonOptions);
        if (data == null) return 0;

        int imported = 0;

        // カテゴリのインポート
        foreach (var category in data.Categories)
        {
            try
            {
                _categoryRepository.Insert(category.Name);
            }
            catch { /* 重複の場合は無視 */ }
        }

        // 定型文のインポート
        foreach (var snippet in data.Snippets)
        {
            if (overwrite)
            {
                // 同じトリガーの既存データを上書き
                var existing = _snippetRepository.GetByTrigger(snippet.TriggerText);
                if (existing != null)
                {
                    snippet.Id = existing.Id;
                    _snippetRepository.Update(snippet);
                    imported++;
                    continue;
                }
            }

            snippet.Id = 0; // 新規として挿入
            _snippetRepository.Insert(snippet);
            imported++;
        }

        return imported;
    }

    /// <summary>データベースのバックアップを作成</summary>
    public async Task BackupAsync(string? backupDir = null)
    {
        backupDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Yobidashi",
            "backups");

        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"yobidashi_backup_{timestamp}.db");

        File.Copy(DatabaseManager.DatabasePath, backupPath, true);

        // 古いバックアップを削除（最新10件のみ保持）
        var backups = Directory.GetFiles(backupDir, "yobidashi_backup_*.db")
            .OrderByDescending(f => f)
            .Skip(10);
        foreach (var old in backups)
        {
            try { File.Delete(old); } catch { }
        }
    }
}
