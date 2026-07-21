namespace VolturaDownloadWatcher.Tests;

public sealed class ExecutableLaunchPolicyTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(9, 0)]
    public void ReadSubsystem_RecognizesPeSubsystem(ushort subsystem, int expected)
    {
        using var stream = CreatePeStream(subsystem);

        Assert.Equal((WindowsExecutableSubsystem)expected, ExecutableLaunchPolicy.ReadSubsystem(stream));
    }

    [Fact]
    public void ReadSubsystem_RejectsInvalidExecutable()
    {
        using var stream = new System.IO.MemoryStream(new byte[128]);

        Assert.Equal(WindowsExecutableSubsystem.Unknown, ExecutableLaunchPolicy.ReadSubsystem(stream));
    }

    [Fact]
    public void CreateStartInfo_LeavesNonExecutablesWithWindowsShell()
    {
        var info = ExecutableLaunchPolicy.CreateStartInfo(@"C:\Downloads\readme.txt");

        Assert.Equal(@"C:\Downloads\readme.txt", info.FileName);
        Assert.True(info.UseShellExecute);
        Assert.Empty(info.Arguments);
    }

    [Fact]
    public void CreateStartInfo_UsesPersistentCommandPromptForConsoleExecutable()
    {
        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid():N}.exe");
        try
        {
            using (var source = CreatePeStream(3))
            using (var destination = System.IO.File.Create(tempPath))
            {
                source.CopyTo(destination);
            }

            var info = ExecutableLaunchPolicy.CreateStartInfo(tempPath);

            Assert.EndsWith("cmd.exe", info.FileName, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/k", info.Arguments, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains(tempPath, info.Arguments, System.StringComparison.Ordinal);
            Assert.Equal(System.IO.Path.GetDirectoryName(tempPath), info.WorkingDirectory);
            Assert.True(info.UseShellExecute);
        }
        finally
        {
            System.IO.File.Delete(tempPath);
        }
    }

    private static System.IO.MemoryStream CreatePeStream(ushort subsystem)
    {
        const int peHeaderOffset = 0x80;
        var bytes = new byte[peHeaderOffset + 24 + 70];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0), 0x5A4D);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3C), peHeaderOffset);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(peHeaderOffset), 0x00004550);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(peHeaderOffset + 24), 0x20B);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(peHeaderOffset + 24 + 68), subsystem);
        return new System.IO.MemoryStream(bytes);
    }
}
