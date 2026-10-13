using System.Text.RegularExpressions;

namespace Callout.Core.Speech;

/// <summary>
/// Cleans Whisper output and rejects the stock phrases it invents on noise
/// (it was trained on subtitles, so it "hears" credits and outros in static).
/// </summary>
public static partial class TranscriptFilter
{
    private static readonly string[] HallucinatedFragments =
    [
        "thanks for watching",
        "thank you for watching",
        "subtitles by",
        "subtitled by",
        "subscribe to",
        "please subscribe",
        "amara.org",
        "dimatorzok",
        "субтитр",
    ];

    private static readonly HashSet<string> HallucinatedWholeLines = new(StringComparer.OrdinalIgnoreCase)
    {
        "you",
        "thank you",
        "bye",
    };

    public static string? Clean(string rawText)
    {
        var text = Whitespace().Replace(SoundAnnotation().Replace(rawText, " "), " ").Trim();

        if (!text.Any(char.IsLetterOrDigit))
        {
            return null;
        }

        if (HallucinatedWholeLines.Contains(text.TrimEnd('.', '!', '?')))
        {
            return null;
        }

        return HallucinatedFragments.Any(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            ? null
            : text;
    }

    [GeneratedRegex(@"\[[^\]]*\]|\([^)]*\)|\*[^*]*\*|♪")]
    private static partial Regex SoundAnnotation();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
