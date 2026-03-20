using System.Text.RegularExpressions;
using System.Windows;

namespace Yobidashi.Core.Expansion;

/// <summary>
/// 変数展開処理
/// 本文中の {{variable}} を実際の値に置換する
/// </summary>
public class VariableProcessor
{
    private readonly Dictionary<string, Func<string>> _builtInVariables;
    private static readonly Regex VariablePattern = new(@"\{\{(\w+(?::[^}]*)?)\}\}", RegexOptions.Compiled);

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

    public (string text, int cursorPosition) Expand(string body)
    {
        if (string.IsNullOrEmpty(body))
            return (body, -1);

        var result = VariablePattern.Replace(body, match =>
        {
            var varName = match.Groups[1].Value;

            if (_builtInVariables.TryGetValue(varName, out var resolver))
            {
                return resolver();
            }

            // 入力型変数（将来実装: {{input:名前}}）
            if (varName.StartsWith("input:", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            return match.Value;
        });

        int cursorPos = result.IndexOf(CursorMarker);
        if (cursorPos >= 0)
        {
            result = result.Replace(CursorMarker, "");
        }

        return (result, cursorPos);
    }

    public string Preview(string body)
    {
        var (text, _) = Expand(body);
        return text;
    }

    private static string GetClipboardText()
    {
        try
        {
            // クリップボード操作はSTAスレッド（UIスレッド）で実行する必要がある
            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                if (Clipboard.ContainsText())
                    return Clipboard.GetText();
            }
            else
            {
                return Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (Clipboard.ContainsText())
                        return Clipboard.GetText();
                    return string.Empty;
                }) ?? string.Empty;
            }
        }
        catch { }
        return string.Empty;
    }

    private static string GetJapaneseWeekday(DateTime dt)
    {
        var weekdays = new[] { "日", "月", "火", "水", "木", "金", "土" };
        return weekdays[(int)dt.DayOfWeek];
    }
}
