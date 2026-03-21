using System.Text;
using Yobidashi.Data.Models;
using Yobidashi.Data.Repositories;

namespace Yobidashi.Core.Services;

/// <summary>
/// トリガー文字列の検知とバッファ管理
/// 確定済み文字列のみをバッファに蓄積し、
/// Space/Tabが押されたときにトリガー判定を行う
///
/// 日本語IME対応:
/// - 各入力セグメントは「画面表示テキスト」と「読みテキスト」を持つ
/// - トリガー照合は読みテキストで行い、削除文字数は画面表示テキストで計算する
/// - 例: "署名"(表示2文字) の読み "しょめい"(4文字) でトリガー判定
/// </summary>
public class TriggerDetector
{
    private readonly List<InputSegment> _segments = new();
    private readonly SnippetRepository _snippetRepository;
    private readonly ITextNormalizer _normalizer;
    private readonly int _maxBufferSize;

    /// <summary>トリガーが一致した時に発火（Snippet, 画面上の削除文字数）</summary>
    public event Action<Snippet, int>? TriggerMatched;

    public TriggerDetector(SnippetRepository snippetRepository, ITextNormalizer normalizer, int maxBufferSize = 100)
    {
        _snippetRepository = snippetRepository;
        _normalizer = normalizer;
        _maxBufferSize = maxBufferSize;
    }

    /// <summary>
    /// 入力セグメント: 画面表示テキストと読み（ひらがな）をペアで保持
    /// </summary>
    private struct InputSegment
    {
        public string DisplayText;  // 画面に表示されるテキスト（漢字等）
        public string ReadingText;  // トリガー照合用の読み（ひらがな）
    }

    /// <summary>
    /// ASCII文字をバッファに追加（直接入力文字）
    /// </summary>
    public void AddChar(char c)
    {
        var s = c.ToString();
        _segments.Add(new InputSegment { DisplayText = s, ReadingText = s });
        TrimBuffer();
    }

    /// <summary>
    /// IME確定テキストをバッファに追加
    /// </summary>
    /// <param name="displayText">画面に表示されるテキスト（例: "署名"）</param>
    /// <param name="readingText">ひらがなの読み（例: "しょめい"）</param>
    public void AddConfirmedText(string displayText, string readingText)
    {
        if (string.IsNullOrEmpty(displayText) && string.IsNullOrEmpty(readingText)) return;
        _segments.Add(new InputSegment
        {
            DisplayText = displayText ?? readingText,
            ReadingText = readingText ?? displayText ?? string.Empty
        });
        TrimBuffer();
    }

    /// <summary>
    /// バッファから最後のセグメントを削除（BackSpace等）
    /// </summary>
    public void RemoveLastChar()
    {
        if (_segments.Count > 0)
        {
            _segments.RemoveAt(_segments.Count - 1);
        }
    }

    /// <summary>
    /// バッファをクリア（フォーカス変更時など）
    /// </summary>
    public void ClearBuffer()
    {
        _segments.Clear();
    }

    /// <summary>
    /// Space/Tabが押されたときにトリガー判定を実行
    /// 読みバッファの末尾からトリガーと照合し、一致したら画面上の削除文字数を返す
    /// </summary>
    public bool CheckTrigger()
    {
        if (_segments.Count == 0) return false;

        // 読みテキストを結合
        var readingBuilder = new StringBuilder();
        foreach (var seg in _segments)
        {
            readingBuilder.Append(seg.ReadingText);
        }
        var readingText = readingBuilder.ToString();

        var enabledSnippets = _snippetRepository.GetEnabled();
        foreach (var snippet in enabledSnippets)
        {
            var trigger = snippet.TriggerText;
            if (string.IsNullOrEmpty(trigger)) continue;

            // 正規化して比較（読みテキスト同士）
            var normalizedReading = _normalizer.Normalize(readingText);
            var normalizedTrigger = _normalizer.Normalize(trigger);

            if (normalizedReading.EndsWith(normalizedTrigger))
            {
                int triggerStart = normalizedReading.Length - normalizedTrigger.Length;

                // 誤爆防止: トリガーの前が行頭、空白、句読点、
                // または非ASCII文字（日本語は単語間にスペースがないため常に許可）
                if (triggerStart == 0 ||
                    char.IsWhiteSpace(normalizedReading[triggerStart - 1]) ||
                    IsPunctuation(normalizedReading[triggerStart - 1]) ||
                    normalizedReading[triggerStart - 1] > 0x7F)
                {
                    // 画面上の削除文字数を計算（セグメントを末尾から遡る）
                    int displayLength = CalculateDisplayLength(normalizedTrigger.Length);
                    TriggerMatched?.Invoke(snippet, displayLength);
                    ClearBuffer();
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 画面上の実テキストからトリガー判定を行う（WM_GETTEXT経由）
    /// IME経由の日本語テキストはキーボードフックでは取得できないため、
    /// 画面上の確定済みテキストから直接照合する
    /// </summary>
    /// <param name="textBeforeCursor">カーソル位置より前のテキスト</param>
    /// <returns>一致したスニペットと削除文字数、一致なしならnull</returns>
    public (Snippet snippet, int displayLength)? CheckTriggerFromScreenText(string textBeforeCursor)
    {
        if (string.IsNullOrEmpty(textBeforeCursor)) return null;

        var enabledSnippets = _snippetRepository.GetEnabled();
        foreach (var snippet in enabledSnippets)
        {
            var trigger = snippet.TriggerText;
            if (string.IsNullOrEmpty(trigger)) continue;

            var normalizedText = _normalizer.Normalize(textBeforeCursor);
            var normalizedTrigger = _normalizer.Normalize(trigger);

            if (normalizedText.EndsWith(normalizedTrigger))
            {
                int triggerStart = normalizedText.Length - normalizedTrigger.Length;

                // 誤爆防止
                if (triggerStart == 0 ||
                    char.IsWhiteSpace(normalizedText[triggerStart - 1]) ||
                    IsPunctuation(normalizedText[triggerStart - 1]) ||
                    normalizedText[triggerStart - 1] > 0x7F)
                {
                    // 画面テキストの場合、トリガー文字数 = 実際の画面表示文字数
                    int displayLength = trigger.Length;
                    return (snippet, displayLength);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 読み文字数に対応する画面表示文字数を計算
    /// セグメントを末尾から遡って、必要な読み文字数分の表示文字数を合算する
    /// </summary>
    private int CalculateDisplayLength(int readingCharsNeeded)
    {
        int displayLen = 0;
        int readingLen = 0;

        for (int i = _segments.Count - 1; i >= 0 && readingLen < readingCharsNeeded; i--)
        {
            var seg = _segments[i];
            readingLen += seg.ReadingText.Length;
            displayLen += seg.DisplayText.Length;
        }

        return displayLen;
    }

    /// <summary>
    /// バッファサイズを制限
    /// </summary>
    private void TrimBuffer()
    {
        int totalReading = 0;
        foreach (var seg in _segments)
        {
            totalReading += seg.ReadingText.Length;
        }

        while (totalReading > _maxBufferSize && _segments.Count > 0)
        {
            totalReading -= _segments[0].ReadingText.Length;
            _segments.RemoveAt(0);
        }
    }

    /// <summary>
    /// 現在のバッファ内容を取得（デバッグ用）
    /// </summary>
    public string GetBufferContent()
    {
        var sb = new StringBuilder();
        foreach (var seg in _segments)
        {
            sb.Append(seg.ReadingText);
        }
        return sb.ToString();
    }

    private static bool IsPunctuation(char c)
    {
        return char.IsPunctuation(c) || c == '　' || c == '、' || c == '。' || c == '！' || c == '？';
    }
}
