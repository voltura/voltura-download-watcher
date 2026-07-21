namespace VolturaDownloadWatcher.Tests;

public sealed class Sha256CalculatorTests
{
    [Xunit.Fact]
    public async System.Threading.Tasks.Task CalculateStableAsync_ReturnsExpectedDigest()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllTextAsync(path, "voltura");

            var result = await Sha256Calculator.CalculateStableAsync(path, System.Threading.CancellationToken.None);

            Xunit.Assert.True(result.Success);
            Xunit.Assert.Equal(
                "9D467C691ADA67CA02EDCD3F171B1884830549C5028B984A3D6295DC2FE95959",
                result.Hash);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Xunit.Fact]
    public async System.Threading.Tasks.Task CalculateStableAsync_ReturnsUnavailableWhenFileIsMissing()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid():N}.missing");

        var result = await Sha256Calculator.CalculateStableAsync(path, System.Threading.CancellationToken.None);

        Xunit.Assert.False(result.Success);
        Xunit.Assert.Null(result.Hash);
    }
}
