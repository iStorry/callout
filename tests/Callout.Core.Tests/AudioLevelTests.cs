using Callout.Core.Speech;

namespace Callout.Core.Tests;

public class AudioLevelTests
{
    [Fact]
    public void Rms_OfConstantSignal_IsItsMagnitude()
    {
        Assert.Equal(0.5f, AudioLevel.Rms([0.5f, -0.5f, 0.5f, -0.5f]), precision: 5);
    }

    [Fact]
    public void Rms_OfNothing_IsZero()
    {
        Assert.Equal(0f, AudioLevel.Rms([]));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.0001f, 0)]
    [InlineData(0.001f, 0)]
    [InlineData(0.01f, 1.0 / 3)]
    [InlineData(0.1f, 2.0 / 3)]
    [InlineData(1f, 1)]
    [InlineData(2f, 1)]
    public void ToMeter_MapsSixtyDecibelsOntoTheMeter(float rms, double expected)
    {
        Assert.Equal(expected, AudioLevel.ToMeter(rms), precision: 3);
    }
}
