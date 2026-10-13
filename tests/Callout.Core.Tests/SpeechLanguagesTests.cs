using Callout.Core.Speech;

namespace Callout.Core.Tests;

public class SpeechLanguagesTests
{
    [Fact]
    public void Codes_AreUniqueLowercaseWhisperCodes()
    {
        var codes = SpeechLanguages.Pinnable.Select(language => language.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[a-z]{2,3}$", code));
    }

    [Fact]
    public void English_IsNotPinnable()
    {
        Assert.DoesNotContain(SpeechLanguages.Pinnable, language => language.Code == SpeechLanguages.EnglishCode);
    }

    [Fact]
    public void Names_AreSortedForThePicker()
    {
        var names = SpeechLanguages.Pinnable.Select(language => language.Name).ToList();

        Assert.Equal(names.Order(StringComparer.Ordinal), names);
    }
}
