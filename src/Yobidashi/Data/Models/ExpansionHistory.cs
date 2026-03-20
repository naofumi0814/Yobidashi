namespace Yobidashi.Data.Models;

public class ExpansionHistory
{
    public long Id { get; set; }
    public long SnippetId { get; set; }
    public DateTime ExpandedAt { get; set; } = DateTime.Now;
    public string AppName { get; set; } = string.Empty;
}
