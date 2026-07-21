namespace VolturaDownloadWatcher;

internal enum WindowsExecutableSubsystem
{
    Unknown,
    WindowsGui,
    WindowsConsole
}

internal static class ExecutableLaunchPolicy
{
    private const ushort Pe32Magic = 0x10B;
    private const ushort Pe32PlusMagic = 0x20B;
    private const ushort WindowsGuiSubsystem = 2;
    private const ushort WindowsConsoleSubsystem = 3;

    internal static System.Diagnostics.ProcessStartInfo CreateStartInfo(string path)
    {
        if (GetSubsystem(path) == WindowsExecutableSubsystem.WindowsConsole)
        {
            var commandProcessor = System.Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrWhiteSpace(commandProcessor))
            {
                commandProcessor = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.System),
                    "cmd.exe");
            }

            return new System.Diagnostics.ProcessStartInfo
            {
                FileName = commandProcessor,
                Arguments = $"/d /s /k \"\"{path}\"\"",
                WorkingDirectory = System.IO.Path.GetDirectoryName(path) ?? string.Empty,
                UseShellExecute = true
            };
        }

        return new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };
    }

    internal static WindowsExecutableSubsystem GetSubsystem(string path)
    {
        if (!string.Equals(System.IO.Path.GetExtension(path), ".exe", System.StringComparison.OrdinalIgnoreCase))
        {
            return WindowsExecutableSubsystem.Unknown;
        }

        try
        {
            using var stream = new System.IO.FileStream(
                path,
                System.IO.FileMode.Open,
                System.IO.FileAccess.Read,
                System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
            return ReadSubsystem(stream);
        }
        catch (System.IO.IOException)
        {
            return WindowsExecutableSubsystem.Unknown;
        }
        catch (System.UnauthorizedAccessException)
        {
            return WindowsExecutableSubsystem.Unknown;
        }
    }

    internal static WindowsExecutableSubsystem ReadSubsystem(System.IO.Stream stream)
    {
        if (!stream.CanRead || !stream.CanSeek || stream.Length < 64)
        {
            return WindowsExecutableSubsystem.Unknown;
        }

        using var reader = new System.IO.BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        stream.Position = 0;
        if (reader.ReadUInt16() != 0x5A4D)
        {
            return WindowsExecutableSubsystem.Unknown;
        }

        stream.Position = 0x3C;
        var peHeaderOffset = reader.ReadInt32();
        const int subsystemOffsetFromPeHeader = 24 + 68;
        if (peHeaderOffset < 0 || peHeaderOffset > stream.Length - subsystemOffsetFromPeHeader - sizeof(ushort))
        {
            return WindowsExecutableSubsystem.Unknown;
        }

        stream.Position = peHeaderOffset;
        if (reader.ReadUInt32() != 0x00004550)
        {
            return WindowsExecutableSubsystem.Unknown;
        }

        stream.Position = peHeaderOffset + 24;
        var optionalHeaderMagic = reader.ReadUInt16();
        if (optionalHeaderMagic != Pe32Magic && optionalHeaderMagic != Pe32PlusMagic)
        {
            return WindowsExecutableSubsystem.Unknown;
        }

        stream.Position = peHeaderOffset + subsystemOffsetFromPeHeader;
        return reader.ReadUInt16() switch
        {
            WindowsGuiSubsystem => WindowsExecutableSubsystem.WindowsGui,
            WindowsConsoleSubsystem => WindowsExecutableSubsystem.WindowsConsole,
            _ => WindowsExecutableSubsystem.Unknown
        };
    }
}
