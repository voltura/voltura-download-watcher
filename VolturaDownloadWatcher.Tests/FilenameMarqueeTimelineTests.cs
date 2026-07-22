namespace VolturaDownloadWatcher.Tests;

public sealed class FilenameMarqueeTimelineTests
{
    [Theory]
    [InlineData(36, 0.5)]
    [InlineData(72, 1)]
    [InlineData(144, 2)]
    public void Create_UsesConstantPixelSpeedAndTwoSecondEdgePauses(
        double overflowPixels,
        double expectedTravelSeconds)
    {
        var timeline = FilenameMarqueeTimeline.Create(overflowPixels);

        Assert.Equal(System.TimeSpan.FromSeconds(2), timeline.StartPauseEnd);
        Assert.Equal(expectedTravelSeconds, (timeline.ForwardEnd - timeline.StartPauseEnd).TotalSeconds, 6);
        Assert.Equal(2, (timeline.FarEdgePauseEnd - timeline.ForwardEnd).TotalSeconds, 6);
        Assert.Equal(expectedTravelSeconds, (timeline.ReturnEnd - timeline.FarEdgePauseEnd).TotalSeconds, 6);
        Assert.Equal(2, (timeline.CycleEnd - timeline.ReturnEnd).TotalSeconds, 6);
    }
}
