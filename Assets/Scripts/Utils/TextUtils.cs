using System.Text.RegularExpressions;

public static class TextUtils
{
    // Hapus tag ekspresi
    public static string StripExpressionTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = Regex.Replace(
            text,
            @"\[[A-Z_]+\]\s*",
            string.Empty,
            RegexOptions.IgnoreCase
        ).Trim();
        return text;
    }

    // Cari tag pertama
    public static string ExtractExpressionTag(string text)
    {
        if (string.IsNullOrEmpty(text)) return "[NEUTRAL]";
        var match = Regex.Match(
            text,
            @"\[(HAPPY|SAD|SURPRISED|THINKING|SHY|NEUTRAL)\]",
            RegexOptions.IgnoreCase
        );
        return match.Success ? match.Value.ToUpper() : "[NEUTRAL]";
    }

    // Hapus emoji dan non-standar
    public static string StripEmoji(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = Regex.Replace(text, @"[☀-➿]", ""); // simbol misc
        text = Regex.Replace(text, @"[\uD800-\uDFFF]", ""); // surrogate pairs
        text = Regex.Replace(text, @"[⌀-⏿]", ""); // simbol teknis
        text = Regex.Replace(text, @"[■-◿]", ""); // geometric shapes
        text = Regex.Replace(text, @"[✀-➿]", ""); // dingbats
        text = Regex.Replace(text, @"\p{Cs}", "");           // surrogate chars
        text = Regex.Replace(text, @"\p{Co}", "");           // private use
        text = Regex.Replace(text, @"\p{Cn}", "");           // unassigned

        return text.Trim();
    }

    // Bersihkan hasil STT
    public static string CleanSTTResult(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = Regex.Replace(text, @"\p{Cs}|\p{So}", "");
        text = Regex.Replace(text, @"[^\w\s\.,!?'\-]", "");
        return text.Trim();
    }
}
