using System.Globalization;
using FuzzySharp;

namespace GameCheatsManager.WinUI.Services;

public static class StringUtilities
{
    public static bool IsChinese(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var character in text)
        {
            if (character >= 0x4E00 && character <= 0x9FFF)
            {
                return true;
            }
        }

        return false;
    }

    public static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var replacedDigits = System.Text.RegularExpressions.Regex.Replace(
            text,
            "\\d+",
            static match => TryConvertUnicodeDigits(match.Value, out var number)
                ? ArabicToRoman(number)
                : match.Value);

        var builder = new System.Text.StringBuilder(replacedDigits.Length);
        foreach (var character in replacedDigits)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            if (character == '&')
            {
                builder.Append(character);
                continue;
            }

            if (char.IsPunctuation(character) || char.IsSymbol(character))
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    public static bool KeywordMatch(IEnumerable<string> keywords, string target)
    {
        var sanitizedTarget = Sanitize(target);
        if (sanitizedTarget.Length < 2)
        {
            return false;
        }

        foreach (var keyword in keywords)
        {
            var sanitizedKeyword = Sanitize(keyword);
            if (sanitizedKeyword.Length < 2)
            {
                continue;
            }

            if (Fuzz.PartialRatio(sanitizedKeyword, sanitizedTarget) >= 80)
            {
                return true;
            }
        }

        return false;
    }

    public static string SymbolReplacement(string text) =>
        text.Replace(": ", " - ", StringComparison.Ordinal)
            .Replace(":", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("?", string.Empty, StringComparison.Ordinal);

    public static int CompareDisplayNames(string? left, string? right, string language)
    {
        var culture = language switch
        {
            "zh_CN" => new CultureInfo("zh-CN"),
            "zh_TW" => new CultureInfo("zh-TW"),
            "de_DE" => new CultureInfo("de-DE"),
            _ => new CultureInfo("en-US")
        };

        return culture.CompareInfo.Compare(left ?? string.Empty, right ?? string.Empty, CompareOptions.StringSort);
    }

    private static string ArabicToRoman(int number)
    {
        if (number == 0)
        {
            return "0";
        }

        var numerals = new (int value, string text)[]
        {
            (1000, "M"),
            (900, "CM"),
            (500, "D"),
            (400, "CD"),
            (100, "C"),
            (90, "XC"),
            (50, "L"),
            (40, "XL"),
            (10, "X"),
            (9, "IX"),
            (5, "V"),
            (4, "IV"),
            (1, "I")
        };

        var builder = new System.Text.StringBuilder();
        var remaining = number;
        foreach (var (value, text) in numerals)
        {
            while (remaining >= value)
            {
                builder.Append(text);
                remaining -= value;
            }
        }

        return builder.ToString();
    }

    private static bool TryConvertUnicodeDigits(string value, out int number)
    {
        number = 0;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            var digit = CharUnicodeInfo.GetDecimalDigitValue(character);
            if (digit < 0)
            {
                number = 0;
                return false;
            }

            try
            {
                checked
                {
                    number = (number * 10) + digit;
                }
            }
            catch (OverflowException)
            {
                number = 0;
                return false;
            }
        }

        return true;
    }
}
