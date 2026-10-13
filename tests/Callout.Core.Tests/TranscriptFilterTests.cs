using Callout.Core.Speech;

namespace Callout.Core.Tests;

public class TranscriptFilterTests
{
    [Theory]
    [InlineData(" Two on B, one is low.", "Two on B, one is low.")]
    [InlineData("Rotate   to  A\n now", "Rotate to A now")]
    [InlineData("[laughs] He's on site", "He's on site")]
    [InlineData("Push (inaudible) mid", "Push mid")]
    public void KeepsRealSpeech(string raw, string expected)
    {
        Assert.Equal(expected, TranscriptFilter.Clean(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("[Music]")]
    [InlineData("♪ ♪")]
    [InlineData("Thank you.")]
    [InlineData(" you")]
    [InlineData("Thanks for watching!")]
    [InlineData("Subtitles by DimaTorzok")]
    [InlineData("Субтитры сделал DimaTorzok")]
    public void RejectsNoiseAndHallucinations(string raw)
    {
        Assert.Null(TranscriptFilter.Clean(raw));
    }
}
