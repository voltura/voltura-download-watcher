namespace VolturaDownloadWatcher.Tests;

public sealed class RunningExecutableActivatorTests
{
    [Theory]
    [InlineData(@"C:\Tools\procexp.exe", @"C:\Tools\procexp.exe", true)]
    [InlineData(@"C:\Tools\procexp.exe", @"C:\Tools\procexp64.exe", true)]
    [InlineData(@"C:\Tools\utility-x86.exe", @"C:\Tools\utility.exe", true)]
    [InlineData(@"C:\Tools\procexp.exe", @"D:\Tools\procexp64.exe", false)]
    [InlineData(@"C:\Tools\procexp.exe", @"C:\Tools\procmon.exe", false)]
    public void AreSameExecutableFamily_RecognizesExactAndArchitectureVariantPaths(
        string requestedPath,
        string runningPath,
        bool expected)
    {
        Assert.Equal(expected, RunningExecutableActivator.AreSameExecutableFamily(requestedPath, runningPath));
    }

    [Fact]
    public void IsDescendantOf_FollowsLauncherChildChain()
    {
        var parentProcessIds = new System.Collections.Generic.Dictionary<int, int>
        {
            [8500] = 10688,
            [10688] = 23116
        };

        Assert.True(RunningExecutableActivator.IsDescendantOf(8500, 10688, parentProcessIds));
        Assert.True(RunningExecutableActivator.IsDescendantOf(8500, 23116, parentProcessIds));
        Assert.False(RunningExecutableActivator.IsDescendantOf(23116, 10688, parentProcessIds));
    }

    [Fact]
    public void IsDescendantOf_StopsOnMalformedCycle()
    {
        var parentProcessIds = new System.Collections.Generic.Dictionary<int, int>
        {
            [10] = 20,
            [20] = 10
        };

        Assert.False(RunningExecutableActivator.IsDescendantOf(10, 30, parentProcessIds));
    }
}
