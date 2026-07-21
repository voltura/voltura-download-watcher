namespace VolturaDownloadWatcher;

internal static class ActivityLog
{
    private static readonly string LogPath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "VolturaDownloadWatcher",
        "activity.txt");
    private static readonly System.Threading.Channels.Channel<LogRequest> Queue =
        System.Threading.Channels.Channel.CreateUnbounded<LogRequest>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    private static System.DateTime _activeDate;

    static ActivityLog()
    {
        _ = System.Threading.Tasks.Task.Run(ProcessQueueAsync);
    }

    public static string EnsureCurrentFile()
    {
        Queue.Writer.TryWrite(LogRequest.Ensure());
        return LogPath;
    }

    public static async System.Threading.Tasks.Task<string> EnsureCurrentFileAsync()
    {
        var completion = new System.Threading.Tasks.TaskCompletionSource(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        if (!Queue.Writer.TryWrite(LogRequest.Ensure(completion)))
        {
            completion.TrySetResult();
        }

        await completion.Task.ConfigureAwait(false);
        return LogPath;
    }

    public static void WriteDownload(string fileName, long sizeBytes) =>
        Write("download", "watcher", fileName, sizeBytes);

    public static void WriteDeletion(string source, string fileName, long sizeBytes) =>
        Write("delete", source, fileName, sizeBytes);

    public static void WriteInteraction(string action, string fileName, long sizeBytes, string result = "ok") =>
        Write($"interaction:{action}", result, fileName, sizeBytes);

    public static void WriteSettingChange(string setting, string value) =>
        Write($"setting:{setting}", value, string.Empty, 0);

    private static void Write(string activity, string source, string fileName, long sizeBytes)
    {
        Queue.Writer.TryWrite(new LogRequest(
            System.DateTimeOffset.Now,
            activity,
            source,
            fileName,
            System.Math.Max(0, sizeBytes),
            null));
    }

    private static async System.Threading.Tasks.Task ProcessQueueAsync()
    {
        await foreach (var request in Queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        ProcessRequest(request);
                        break;
                    }
                    catch (System.IO.IOException) when (attempt < 2)
                    {
                        await System.Threading.Tasks.Task.Delay(50 * (attempt + 1)).ConfigureAwait(false);
                    }
                    catch (System.UnauthorizedAccessException) when (attempt < 2)
                    {
                        await System.Threading.Tasks.Task.Delay(50 * (attempt + 1)).ConfigureAwait(false);
                    }
                    catch (System.Exception)
                    {
                        break;
                    }
                }
            }
            finally
            {
                request.Completion?.TrySetResult();
            }
        }
    }

    private static void ProcessRequest(LogRequest request)
    {
        EnsureCurrentDay(request.OccurredAt.LocalDateTime.Date);
        if (request.Activity is null)
        {
            if (!System.IO.File.Exists(LogPath))
            {
                System.IO.File.WriteAllText(LogPath, string.Empty, System.Text.Encoding.UTF8);
            }

            return;
        }

        var safeActivity = Sanitize(request.Activity);
        var safeSource = Sanitize(request.Source ?? string.Empty);
        var safeName = Sanitize(request.FileName ?? string.Empty).Replace("\"", "'");
        var line = $"{request.OccurredAt:O}\t{safeActivity}\t{safeSource}\t\"{safeName}\"\t{request.SizeBytes} B{System.Environment.NewLine}";
        System.IO.File.AppendAllText(LogPath, line, System.Text.Encoding.UTF8);
    }

    private static string Sanitize(string value) =>
        value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

    private static void EnsureCurrentDay(System.DateTime today)
    {
        if (_activeDate == today)
        {
            return;
        }

        var directory = System.IO.Path.GetDirectoryName(LogPath)!;
        System.IO.Directory.CreateDirectory(directory);
        if (System.IO.File.Exists(LogPath)
            && System.IO.File.GetLastWriteTime(LogPath).Date != today)
        {
            System.IO.File.Delete(LogPath);
        }

        _activeDate = today;
    }

    private sealed record LogRequest(
        System.DateTimeOffset OccurredAt,
        string? Activity,
        string? Source,
        string? FileName,
        long SizeBytes,
        System.Threading.Tasks.TaskCompletionSource? Completion)
    {
        public static LogRequest Ensure(System.Threading.Tasks.TaskCompletionSource? completion = null) =>
            new(System.DateTimeOffset.Now, null, null, null, 0, completion);
    }
}
