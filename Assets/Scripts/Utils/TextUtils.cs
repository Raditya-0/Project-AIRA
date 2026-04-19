using System.Text.RegularExpressions;

public static class TextUtils
{
    // Hapus tag ekspresi
    public static string StripExpressionTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(
            text,
            @"\[(HAPPY|SAD|SURPRISED|THINKING|SHY|NEUTRAL)\]\s*",
            string.Empty,
            RegexOptions.IgnoreCase
        ).Trim();
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

    // Hapus emoji unicode
    public static string StripEmoji(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(text, @"\p{Cs}|\p{So}", "").Trim();
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
