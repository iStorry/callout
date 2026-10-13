namespace Callout.Core.Speech;

public sealed record SpeechLanguage(string Code, string Name);

/// <summary>
/// Languages a player can pin instead of auto-detection. Auto-detection covers every language
/// Whisper knows; pinning one helps when short callouts get misdetected.
/// </summary>
public static class SpeechLanguages
{
    public const string EnglishCode = "en";

    /// <summary>Whisper language codes, sorted by name. English is absent: it needs no translation.</summary>
    public static IReadOnlyList<SpeechLanguage> Pinnable { get; } =
    [
        new("ar", "Arabic"),
        new("be", "Belarusian"),
        new("bg", "Bulgarian"),
        new("zh", "Chinese"),
        new("hr", "Croatian"),
        new("cs", "Czech"),
        new("da", "Danish"),
        new("nl", "Dutch"),
        new("et", "Estonian"),
        new("fi", "Finnish"),
        new("fr", "French"),
        new("de", "German"),
        new("el", "Greek"),
        new("he", "Hebrew"),
        new("hi", "Hindi"),
        new("hu", "Hungarian"),
        new("id", "Indonesian"),
        new("it", "Italian"),
        new("ja", "Japanese"),
        new("kk", "Kazakh"),
        new("ko", "Korean"),
        new("lv", "Latvian"),
        new("lt", "Lithuanian"),
        new("ms", "Malay"),
        new("no", "Norwegian"),
        new("fa", "Persian"),
        new("pl", "Polish"),
        new("pt", "Portuguese"),
        new("ro", "Romanian"),
        new("ru", "Russian"),
        new("sr", "Serbian"),
        new("sk", "Slovak"),
        new("sl", "Slovenian"),
        new("es", "Spanish"),
        new("sv", "Swedish"),
        new("tl", "Tagalog"),
        new("th", "Thai"),
        new("tr", "Turkish"),
        new("uk", "Ukrainian"),
        new("vi", "Vietnamese"),
    ];
}
