namespace VolturaDownloadWatcher.Tests;

public sealed class DialogPlacementTests
{
    [Xunit.Fact]
    public void CalculateRightCenter_UsesPrimaryWorkingAreaCoordinatesWithoutRightGap()
    {
        var placement = DialogPlacement.CalculateRightCenter(
            new System.Drawing.Rectangle(1920, 100, 2560, 1400),
            new System.Drawing.Size(400, 800));

        Xunit.Assert.Equal(new System.Drawing.Point(4080, 400), placement);
    }

    [Xunit.Fact]
    public void CalculateNear_PlacesDialogAboveAndLeftOfBottomRightTrayClick()
    {
        var placement = DialogPlacement.CalculateNear(
            new System.Drawing.Point(1880, 1040),
            new System.Drawing.Rectangle(0, 0, 1920, 1040),
            new System.Drawing.Size(700, 356));

        Xunit.Assert.Equal(new System.Drawing.Point(1164, 668), placement);
    }

    [Xunit.Fact]
    public void CalculateNear_FlipsAndClampsAtTopLeftEdge()
    {
        var placement = DialogPlacement.CalculateNear(
            new System.Drawing.Point(4, 4),
            new System.Drawing.Rectangle(0, 0, 800, 600),
            new System.Drawing.Size(700, 356));

        Xunit.Assert.Equal(new System.Drawing.Point(20, 20), placement);
    }
}
