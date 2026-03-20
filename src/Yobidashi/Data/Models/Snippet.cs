namespace Yobidashi.Data.Models;

/// <summary>
/// 定型文データモデル
/// </summary>
public class Snippet
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TriggerText { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty; // JSON配列 ["タグ1","タグ2"]
    public string Memo { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int UseCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? LastUsedAt { get; set; }

    /// <summary>タグ文字列をリストに変換</summary>
    public List<string> GetTagList()
    {
        if (string.IsNullOrWhiteSpace(Tags)) return new List<string>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(Tags) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>タグリストをJSON文字列に変換</summary>
    public void SetTagList(List<string> tags)
    {
        Tags = System.Text.Json.JsonSerializer.Serialize(tags);
    }
}
