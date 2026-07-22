namespace VolturaDownloadWatcher.Tests;

public sealed class DownloadNotificationPlacementTests
{
    [Fact]
    public void Calculate_PlacesPanelAboveBottomTaskbarNotificationArea()
    {
        var placement = DownloadNotificationPlacement.Calculate(
            new System.Drawing.Rectangle(0, 0, 1920, 1080),
            new System.Drawing.Rectangle(0, 0, 1920, 1040),
            new System.Drawing.Rectangle(1680, 1040, 240, 40),
            new System.Drawing.Size(286, 108));

        Assert.Equal(new System.Drawing.Point(1634, 922), placement);
    }

    [Fact]
    public void Calculate_PlacesPanelBelowTopTaskbarAndClampsRightEdge()
    {
        var placement = DownloadNotificationPlacement.Calculate(
            new System.Drawing.Rectangle(-1920, 0, 1920, 1080),
            new System.Drawing.Rectangle(-1920, 40, 1920, 1040),
            new System.Drawing.Rectangle(-220, 0, 220, 40),
            new System.Drawing.Size(286, 108));

        Assert.Equal(new System.Drawing.Point(-286, 50), placement);
    }
}
