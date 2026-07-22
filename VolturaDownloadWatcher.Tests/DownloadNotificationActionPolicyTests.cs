namespace VolturaDownloadWatcher.Tests;

public sealed class DownloadNotificationActionPolicyTests
{
    [Theory]
    [InlineData(DownloadNotificationActionOutcome.Succeeded, true)]
    [InlineData(DownloadNotificationActionOutcome.Cancelled, false)]
    [InlineData(DownloadNotificationActionOutcome.Failed, false)]
    public void ShouldDismiss_OnlyAfterSuccessfulAction(
        DownloadNotificationActionOutcome outcome,
        bool expected) =>
        Assert.Equal(expected, DownloadNotificationActionPolicy.ShouldDismiss(outcome));
}
