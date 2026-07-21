namespace VolturaDownloadWatcher.Tests;

public sealed class RenamePolicyTests
{
    [Theory]
    [InlineData("release-notes.txt")]
    [InlineData("VolturaAir-Setup-0.7.1-win-x64.exe")]
    [InlineData("archive.tar.gz")]
    public void IsValidFileName_AcceptsSafeNames(string fileName) =>
        Assert.True(RenamePolicy.IsValidFileName(fileName));

    [Theory]
    [InlineData("")]
    [InlineData("../outside.txt")]
    [InlineData("folder/file.txt")]
    [InlineData("bad:name.txt")]
    [InlineData("trailing. ")]
    [InlineData("CON.txt")]
    [InlineData("LPT9")]
    public void IsValidFileName_RejectsUnsafeNames(string fileName) =>
        Assert.False(RenamePolicy.IsValidFileName(fileName));
}
