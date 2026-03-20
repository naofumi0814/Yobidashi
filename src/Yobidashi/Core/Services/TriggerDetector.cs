using Yobidashi.Data.Models;
using Yobidashi.Data.Repositories;

namespace Yobidashi.Core.Services;

/// <summary>
/// トリガー文字列の検知とバッファ管理
/// 確定済み文字列のみをバッファに蓄積し、
/// Space/Tabが押されたときにトリガー判定を行う
/// </summary>
public class TriggerDetector
{
    private readonly List<char> _buffer = new();
    private readonly SnippetRepository _snippetRepository;
    private readonly ITextNormalizer _normalizer;
    private readonly int _maxBufferSize;

    /// <summary>トリガーが一致した時に発火（Snippet, トリガー文字数）</summary>
    public event Action<Snippet, int>? TriggerMatched;

    public TriggerDetector(SnippetRepository snippetRepository, ITextNormalizer normalizer, int maxBufferSize = 100)
    {
        _snippetRepository = snippetRepository;
        _normalizer = normalizer;
        _maxBufferSize = maxBufferSize;
    }

    /// <summary>
    /// 確定済み文字をバッファに追加
    /// </summary>
    public void AddChar(char c)
    {
        _buffer.Add(c);
        if (_buffer.Count > _maxBufferSize)
        {
            _buffer.RemoveAt(0);
        }
    }

    /// <summary>
    /// バッファからテキストを削除（BackSpace等）
    /// </summary>
    public void RemoveLastChar()
    {
        if (_buffer.Count > 0)
        {
            _buffer.RemoveAt(_buffer.Count - 1);
        }
    }

    /// <summary>
    /// バッファをクリア（フォーカス変更時など）
    /// </summary>
    public void ClearBuffer()
    {
        _buffer.Clear();
    }

    /// <summary>
    /// Space/Tabが押されたときにトリガー判定を実行
    /// バッファの末尾から単語を切り出してトリガーと照合する
    /// </summary>
    public bool CheckTrigger()
    {
        if (_buffer.Count == 0) return false;

        // バッファの内容を文字列として取得
        var bufferText = new string(_buffer.ToArray());

        // 末尾の空白を除去し、最後の「単語」を抽出
        // 日本語の場合、区切り文字なしで連続するため、
        // 登録されたトリガー文字列と末尾マッチで判定する
        var enabledSnippets = _snippetRepository.GetEnabled();
        foreach (var snippet in enabledSnippets)
        {
            var trigger = snippet.TriggerText;
            if (string.IsNullOrEmpty(trigger)) continue;

            // 正規化して比較
            var normalizedBuffer = _normalizer.Normalize(bufferText);
            var normalizedTrigger = _normalizer.Normalize(trigger);

            if (normalizedBuffer.EndsWith(normalizedTrigger))
            {
                // バッファの末尾がトリガーと一致
                int triggerStart = normalizedBuffer.Length - normalizedTrigger.Length;

                // 誤爆防止: トリガーの前が行頭、空白、句読点、
                // または非ASCII文字（日本語は単語間にスペースがないため常に許可）
                if (triggerStart == 0 ||
                    char.IsWhiteSpace(normalizedBuffer[triggerStart - 1]) ||
                    IsPunctuation(normalizedBuffer[triggerStart - 1]) ||
                    normalizedBuffer[triggerStart - 1] > 0x7F)
                {
                    TriggerMatched?.Invoke(snippet, trigger.Length);
                    ClearBuffer();
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 現在のバッファ内容を取得（デバッグ用）
    /// </summary>
    public string GetBufferContent()
    {
        return new string(_buffer.ToArray());
    }

    private static bool IsPunctuation(char c)
    {
        return char.IsPunctuation(c) || c == '　' || c == '、' || c == '。' || c == '！' || c == '？';
    }
}
