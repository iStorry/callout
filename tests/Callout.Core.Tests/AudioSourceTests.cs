using Callout.Core.Audio;

namespace Callout.Core.Tests;

public class AudioSourceTests
{
    [Fact]
    public void ForApp_RoundTripsTheProcessNameThroughItsId()
    {
        var source = AudioSource.ForApp("Discord");

        Assert.Equal(AudioSourceKind.AppLoopback, source.Kind);
        Assert.True(AudioSource.TryGetAppProcessName(source.Id, out var processName));
        Assert.Equal("Discord", processName);
    }

    [Fact]
    public void ForApp_HidesTheUnrealShippingSuffixButKeepsTheRealProcessName()
    {
        var source = AudioSource.ForApp("VALORANT-Win64-Shipping");

        Assert.Equal("App: VALORANT", source.DisplayName);
        Assert.True(AudioSource.TryGetAppProcessName(source.Id, out var processName));
        Assert.Equal("VALORANT-Win64-Shipping", processName);
    }

    [Fact]
    public void ForApp_MarksAppsThatAreNotRunning()
    {
        Assert.Equal("App: cs2 (not running)", AudioSource.ForApp("cs2", isRunning: false).DisplayName);
    }

    [Theory]
    [InlineData("{0.0.0.00000000}.{5c3a9b7e-0000-4000-8000-000000000000}")]
    [InlineData("app:")]
    [InlineData("")]
    public void TryGetAppProcessName_RejectsDeviceIdsAndEmptyNames(string id)
    {
        Assert.False(AudioSource.TryGetAppProcessName(id, out _));
    }
}
