namespace VolturaDownloadWatcher.Tests;

public sealed class DownloadNotificationDurationPolicyTests
{
    [Theory]
    [InlineData(DownloadNotificationDurationPolicy.UntilDismissed)]
    [InlineData(DownloadNotificationDurationPolicy.Off)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(600)]
    public void NormalizePersisted_PreservesSupportedValues(int seconds) =>
        Assert.Equal(seconds, DownloadNotificationDurationPolicy.NormalizePersisted(seconds));

    [Theory]
    [InlineData(-2)]
    [InlineData(601)]
    [InlineData(int.MaxValue)]
    public void NormalizePersisted_FallsBackToTenSeconds(int seconds) =>
        Assert.Equal(10, DownloadNotificationDurationPolicy.NormalizePersisted(seconds));

    [Theory]
    [InlineData(-20, 1)]
    [InlineData(0, 1)]
    [InlineData(37, 37)]
    [InlineData(999, 600)]
    public void NormalizeCustom_ClampsToSupportedRange(int seconds, int expected) =>
        Assert.Equal(expected, DownloadNotificationDurationPolicy.NormalizeCustom(seconds));

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(37, false)]
    public void IsPreset_IdentifiesMenuPresets(int seconds, bool expected) =>
        Assert.Equal(expected, DownloadNotificationDurationPolicy.IsPreset(seconds));
}
