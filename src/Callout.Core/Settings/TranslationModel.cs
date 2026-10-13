namespace Callout.Core.Settings;

/// <summary>
/// Whisper sizes that can translate. Large-v3-turbo is deliberately absent:
/// it was trained without translation data and echoes the original language instead of English.
/// </summary>
public enum TranslationModel
{
    Small,
    Medium,
    LargeV3,
}
