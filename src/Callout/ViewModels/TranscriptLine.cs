namespace Callout.ViewModels;

/// <param name="Language">Whisper code of the language the line was spoken in.</param>
public sealed record TranscriptLine(string Text, string Language, DateTimeOffset ReceivedAt)
{
    public string LanguageTag => Language.ToUpperInvariant();
}

/// <summary>Somewhere translated lines are shown: the chat window or the in-game overlay.</summary>
internal interface ITranscriptSurface
{
    void Show(TranscriptLine line);
}
