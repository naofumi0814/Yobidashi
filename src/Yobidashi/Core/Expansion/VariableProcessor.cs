using System.Text.RegularExpressions;

namespace Yobidashi.Core.Expansion;

/// <summary>
/// 変数展開処理
/// 本文中の {{variable}} を実際の値に置換する
/// </summary>
public class VariableProcessor
{
    private readonly Dictionary<string, Func<string>> _builtInVariables;
    private static readonly Regex VariablePattern = new(@"\{\{(\w+(?::[^}]*)?)\}\}", RegexOptions.Compiled);

    /// <summary>カーソル位置マーカー（展開後にカーソルをこの位置に移動）</summary>
    public const string CursorMarker = "\x00CURSOR\x00";

    public VariableProcessor()
    {
        _builtInVariables = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["date"] = () => DateTime.Now.ToString("yyyy/MM/dd"),
            ["time"] = () => DateTime.Now.ToString("HH:mm"),
            ["datetime"] = () => DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
            ["date_jp"] = () => DateTime.Now.ToString("yyyy年MM月dd日"),
            ["time_jp"] = () => DateTime.Now.ToString("HH時mm分"),
            ["weekday"] = () => GetJapaneseWeekday(DateTime.Now),
            ["clipboard"] = GetClipboardText,
            ["cursor"] = () => CursorMarker,
        };
    }

    /// <summary>
    /// 本文中の変数を展開する
    /// </summary>
    /// <returns>展開後の文字列と、カーソル位置（見つかった場合）</returns>
    public (string text, int cursorPosition) Expand(string body)
    {
        if (string.IsNullOrEmpty(body))
            return (body, -1);

        var result = VariablePattern.Replace(body, match =>
        {
            var varName = match.Groups[1].Value;

            // 組み込み変数
            if (_builtInVariables.TryGetValue(varName, out var resolver))
            {
                return resolver();
            }

            // 入力型変数（将来実装: {{input:名前}}）
            if (varName.StartsWith("input:", StringComparison.OrdinalIgnoreCase))
            {
                // MVPでは未実装、プレースホルダをそのまま残す
                return match.Value;
            }

            // 未知の変数はそのまま残す
            return match.Value;
        });

        // カーソル位置の検出
        int cursorPos = result.IndexOf(CursorMarker);
        if (cursorPos >= 0)
        {
            result = result.Replace(CursorMarker, "");
        }

        return (result, cursorPos);
    }

    /// <summary>変数プレビュー用（編集画面で使用）</summary>
    public string Preview(string body)
    {
        var (text, _) = Expand(body);
        return text;
    }

    private static string GetClipboardText()
    {
        try
        {
            // WinUI 3環境でのクリップボードアクセス
            var package = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (package.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                return package.GetTextAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        catch
        {
            // クリップボードアクセス失敗時は空文字
        }
        return string.Empty;
    }

    private static string GetJapaneseWeekday(DateTime dt)
    {
        var weekdays = new[] { "日", "月", "火", "水", "木", "金", "土" };
        return weekdays[(int)dt.DayOfWeek];
    }
}
