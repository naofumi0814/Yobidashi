namespace Yobidashi.Core.Services;

/// <summary>
/// 文字列正規化サービス
/// 日本語入力で起こりやすい表記ゆれに対応するためのレイヤー
/// </summary>
public interface ITextNormalizer
{
    /// <summary>比較用に正規化した文字列を返す</summary>
    string Normalize(string input);
}

/// <summary>
/// MVP版: 基本的な正規化のみ実装
/// 将来的に全角半角変換、ひらがな⇔カタカナ等を追加予定
/// </summary>
public class TextNormalizer : ITextNormalizer
{
    public string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // 前後の空白除去
        var result = input.Trim();

        // 大文字小文字の統一（英数字部分）
        result = result.ToLowerInvariant();

        // 全角英数字 → 半角英数字
        result = NormalizeAlphanumeric(result);

        return result;
    }

    /// <summary>
    /// 全角英数字を半角に変換
    /// </summary>
    private string NormalizeAlphanumeric(string input)
    {
        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            // 全角数字 (０-９) → 半角 (0-9)
            if (chars[i] >= '\uFF10' && chars[i] <= '\uFF19')
            {
                chars[i] = (char)(chars[i] - '\uFF10' + '0');
            }
            // 全角大文字 (Ａ-Ｚ) → 半角小文字 (a-z)
            else if (chars[i] >= '\uFF21' && chars[i] <= '\uFF3A')
            {
                chars[i] = (char)(chars[i] - '\uFF21' + 'a');
            }
            // 全角小文字 (ａ-ｚ) → 半角小文字 (a-z)
            else if (chars[i] >= '\uFF41' && chars[i] <= '\uFF5A')
            {
                chars[i] = (char)(chars[i] - '\uFF41' + 'a');
            }
        }
        return new string(chars);
    }

    // 将来の拡張用メソッド

    /// <summary>カタカナをひらがなに変換（将来実装）</summary>
    public static string KatakanaToHiragana(string input)
    {
        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            // カタカナ (ァ-ヶ) → ひらがな (ぁ-ゖ)
            if (chars[i] >= '\u30A1' && chars[i] <= '\u30F6')
            {
                chars[i] = (char)(chars[i] - 0x60);
            }
        }
        return new string(chars);
    }

    /// <summary>ひらがなをカタカナに変換（将来実装）</summary>
    public static string HiraganaToKatakana(string input)
    {
        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            // ひらがな (ぁ-ゖ) → カタカナ (ァ-ヶ)
            if (chars[i] >= '\u3041' && chars[i] <= '\u3096')
            {
                chars[i] = (char)(chars[i] + 0x60);
            }
        }
        return new string(chars);
    }
}
