using UnityEngine;
using System.Text;

/// <summary>
/// 文本消毒工具：替换 TMP 字体可能不支持的字符，防止乱码/方块。
/// 在 DialogueManager 显示前调用。
/// </summary>
public static class TextSanitizer
{
    /// <summary>
    /// 对显示文本做安全替换，返回清理后的字符串。
    /// 主要处理 CJK 字体中常见的缺失字符（花引号、特殊破折号等）。
    /// </summary>
    public static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var sb = new StringBuilder(text.Length);

        foreach (char c in text)
        {
            sb.Append(MapChar(c));
        }

        return sb.ToString();
    }

    static char MapChar(char c)
    {
        switch (c)
        {
            // ====== 花引号 → ASCII 引号（CJK 字体常缺失） ======
            case '“': // " LEFT DOUBLE QUOTATION MARK
            case '”': // " RIGHT DOUBLE QUOTATION MARK
                return '"';

            case '‘': // ' LEFT SINGLE QUOTATION MARK
            case '’': // ' RIGHT SINGLE QUOTATION MARK
                return '\'';

            // ====== 破折号 → 两个减号 ======
            case '—': // — EM DASH
                return '—'; // ← 保留，大部分 CJK 字体都支持。如需替换，改为 '-'

            // ====== 省略号 → 三个点 ======
            case '…': // … HORIZONTAL ELLIPSIS
                return '…'; // ← 保留，CJK 常用。如需替换，改为 '…'→'...' 需变 String

            // ====== 全角数字/英文 → 半角 ======
            case 'Ａ': case 'Ｂ': case 'Ｃ': case 'Ｄ': case 'Ｅ':
            case 'Ｆ': case 'Ｇ': case 'Ｈ': case 'Ｉ': case 'Ｊ':
            case 'Ｋ': case 'Ｌ': case 'Ｍ': case 'Ｎ': case 'Ｏ':
            case 'Ｐ': case 'Ｑ': case 'Ｒ': case 'Ｓ': case 'Ｔ':
            case 'Ｕ': case 'Ｖ': case 'Ｗ': case 'Ｘ': case 'Ｙ':
            case 'Ｚ':
                return (char)(c - 0xFF21 + 'A');

            case 'ａ': case 'ｂ': case 'ｃ': case 'ｄ': case 'ｅ':
            case 'ｆ': case 'ｇ': case 'ｈ': case 'ｉ': case 'ｊ':
            case 'ｋ': case 'ｌ': case 'ｍ': case 'ｎ': case 'ｏ':
            case 'ｐ': case 'ｑ': case 'ｒ': case 'ｓ': case 'ｔ':
            case 'ｕ': case 'ｖ': case 'ｗ': case 'ｘ': case 'ｙ':
            case 'ｚ':
                return (char)(c - 0xFF41 + 'a');

            case '０': case '１': case '２': case '３': case '４':
            case '５': case '６': case '７': case '８': case '９':
                return (char)(c - 0xFF10 + '0');

            default:
                return c;
        }
    }

    /// <summary>
    /// 对于省略号这种需要多字符替换的情况，用 string 级别处理。
    /// </summary>
    public static string SanitizeString(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // 先做字符级映射
        text = Sanitize(text);

        // 如果需要把 … 替换为 ...
        // text = text.Replace('…'.ToString(), "...");

        // 全角空格 → 半角空格
        text = text.Replace('　', ' ');

        return text;
    }
}
