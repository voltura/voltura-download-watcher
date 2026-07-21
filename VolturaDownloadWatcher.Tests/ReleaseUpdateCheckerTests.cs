namespace VolturaDownloadWatcher.Tests;

public sealed class ReleaseUpdateCheckerTests
{
    [Xunit.Theory]
    [Xunit.InlineData("v0.2.0", "0.1.9", true)]
    [Xunit.InlineData("0.1.0", "0.1.0", false)]
    [Xunit.InlineData("v0.1.0-beta.1", "0.0.9", true)]
    [Xunit.InlineData("not-a-version", "0.1.0", false)]
    public void IsNewer_ComparesReleaseTags(string candidate, string running, bool expected)
    {
        Xunit.Assert.Equal(expected, ReleaseUpdateChecker.IsNewer(candidate, running));
    }

    [Xunit.Theory]
    [Xunit.InlineData("v1.2.3", "1.2.3")]
    [Xunit.InlineData("V2.0.0+build.5", "2.0.0")]
    [Xunit.InlineData(" 3.4.5-beta ", "3.4.5")]
    public void NormalizeVersionText_RemovesTagDecoration(string input, string expected)
    {
        Xunit.Assert.Equal(expected, ReleaseUpdateChecker.NormalizeVersionText(input));
    }
}
