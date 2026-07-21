namespace VolturaDownloadWatcher.Tests;

public sealed class DownloadDefaultActionPolicyTests
{
    [Xunit.Theory]
    [Xunit.InlineData(DownloadDefaultAction.OpenFile)]
    [Xunit.InlineData(DownloadDefaultAction.ShowInExplorer)]
    [Xunit.InlineData(DownloadDefaultAction.CopyAsPath)]
    [Xunit.InlineData(DownloadDefaultAction.CopyFile)]
    [Xunit.InlineData(DownloadDefaultAction.CutFile)]
    public void Normalize_PreservesKnownActions(DownloadDefaultAction action) =>
        Xunit.Assert.Equal(action, DownloadDefaultActionPolicy.Normalize(action));

    [Xunit.Fact]
    public void Normalize_FallsBackToOpenFileForUnknownValue() =>
        Xunit.Assert.Equal(
            DownloadDefaultAction.OpenFile,
            DownloadDefaultActionPolicy.Normalize((DownloadDefaultAction)999));
}
