using System.Windows;

namespace Yobidashi.Core.Services;

/// <summary>
/// クリップボード履歴サービス
/// クリップボードの変更を監視し、テキスト履歴を最大100件保持する
/// </summary>
public class ClipboardHistoryService
{
    private readonly List<ClipboardEntry> _history = new();
    private readonly object _lock = new();
    private const int MaxHistory = 100;
    private string _lastText = string.Empty;

    /// <summary>クリップボード履歴エントリ</summary>
    public class ClipboardEntry
    {
        public string Text { get; set; } = string.Empty;
        public DateTime CopiedAt { get; set; } = DateTime.Now;

        /// <summary>表示用の短縮テキスト（1行目のみ）</summary>
        public string DisplayText
        {
            get
            {
                var firstLine = Text.Split('\n')[0].Trim();
                return firstLine.Length > 120 ? firstLine[..120] + "..." : firstLine;
            }
        }

        /// <summary>表示用の時刻</summary>
        public string TimeText => CopiedAt.ToString("HH:mm");
    }

    /// <summary>クリップボード変更を検知した時に呼ぶ</summary>
    public void OnClipboardChanged()
    {
        try
        {
            if (!Clipboard.ContainsText()) return;

            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (text == _lastText) return;

            _lastText = text;

            lock (_lock)
            {
                // 重複を除去（同じテキストが既にあれば先頭に移動）
                _history.RemoveAll(e => e.Text == text);

                _history.Insert(0, new ClipboardEntry
                {
                    Text = text,
                    CopiedAt = DateTime.Now
                });

                // 最大件数を超えたら古いものを削除
                while (_history.Count > MaxHistory)
                {
                    _history.RemoveAt(_history.Count - 1);
                }
            }
        }
        catch
        {
            // クリップボードアクセス失敗は無視
        }
    }

    /// <summary>履歴を取得（最新順）</summary>
    public List<ClipboardEntry> GetHistory()
    {
        lock (_lock)
        {
            return new List<ClipboardEntry>(_history);
        }
    }

    /// <summary>履歴を検索</summary>
    public List<ClipboardEntry> Search(string query)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<ClipboardEntry>(_history);

            return _history
                .Where(e => e.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
