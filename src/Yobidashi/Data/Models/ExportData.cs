namespace Yobidashi.Data.Models;

/// <summary>
/// エクスポート/インポート用のデータ構造
/// </summary>
public class ExportData
{
    public string Version { get; set; } = "1.0";
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public List<Snippet> Snippets { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
}
