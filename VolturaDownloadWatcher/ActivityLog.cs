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

    public static void WriteLifecycle(string state) =>
        Write("lifecycle", state, string.Empty, 0);

    public static System.Threading.Tasks.Task FlushAsync()
    {
        var completion = new System.Threading.Tasks.TaskCompletionSource(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        if (!Queue.Writer.TryWrite(LogRequest.Ensure(completion)))
        {
            completion.TrySetResult();
        }

        return completion.Task;
    }

    public static async System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<LoggedDownload>> ReadRecentDownloadsAsync(
        int maximumCount)
    {
        await EnsureCurrentFileAsync().ConfigureAwait(false);
        return await System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var stream = new System.IO.FileStream(
                    LogPath,
                    System.IO.FileMode.Open,
                    System.IO.FileAccess.Read,
                    System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
                using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, true);
                var lines = new System.Collections.Generic.List<string>();
                while (reader.ReadLine() is { } line)
                {
                    lines.Add(line);
                }

                return ParseRecentDownloads(lines, maximumCount);
            }
            catch (System.Exception)
            {
                return System.Array.Empty<LoggedDownload>();
            }
        }).ConfigureAwait(false);
    }

    internal static System.Collections.Generic.IReadOnlyList<LoggedDownload> ParseRecentDownloads(
        System.Collections.Generic.IEnumerable<string> lines,
        int maximumCount)
    {
        var latestByName = new System.Collections.Generic.Dictionary<string, MutableLoggedDownload>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            if (fields.Length < 5
                || !System.DateTimeOffset.TryParse(
                    fields[0],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var occurredAt))
            {
                continue;
            }

            var activity = fields[1];
            var source = fields[2];
            var fileName = fields[3].Length >= 2 && fields[3][0] == '"' && fields[3][^1] == '"'
                ? fields[3][1..^1]
                : fields[3];
            var sizeText = fields[4].Split(' ', 2)[0];
            _ = long.TryParse(sizeText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var sizeBytes);

            if (activity == "download" && DownloadPolicy.IsValidDownloadName(fileName))
            {
                latestByName[fileName] = new MutableLoggedDownload(fileName, occurredAt, System.Math.Max(0, sizeBytes));
                continue;
            }

            if ((activity == "interaction:rename" || activity == "interaction:rename-overwrite")
                && source == "ok")
            {
                var separator = fileName.IndexOf(" -> ", System.StringComparison.Ordinal);
                if (separator > 0)
                {
                    var oldName = fileName[..separator];
                    var newName = fileName[(separator + 4)..];
                    if (latestByName.Remove(oldName, out var renamed) && DownloadPolicy.IsValidDownloadName(newName))
                    {
                        renamed.FileName = newName;
                        renamed.SizeBytes = System.Math.Max(renamed.SizeBytes, sizeBytes);
                        latestByName[newName] = renamed;
                    }
                }

                continue;
            }

            if (activity == "delete" && latestByName.TryGetValue(fileName, out var deleted))
            {
                deleted.DeletedAt = occurredAt;
                deleted.SizeBytes = System.Math.Max(deleted.SizeBytes, sizeBytes);
            }

            if (activity == "interaction:sha256-calculated"
                && latestByName.TryGetValue(fileName, out var hashed)
                && source.StartsWith("ok;sha256=", System.StringComparison.Ordinal))
            {
                var hash = source["ok;sha256=".Length..];
                if (hash.Length == 64 && hash.All(System.Uri.IsHexDigit))
                {
                    hashed.Sha256 = hash.ToUpperInvariant();
                }
            }
        }

        return latestByName.Values
            .OrderByDescending(download => download.DownloadedAt)
            .Take(System.Math.Max(0, maximumCount))
            .Select(download => new LoggedDownload(
                download.FileName,
                download.DownloadedAt,
                download.SizeBytes,
                download.DeletedAt,
                download.Sha256))
            .ToArray();
    }

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

    internal sealed record LoggedDownload(
        string FileName,
        System.DateTimeOffset DownloadedAt,
        long SizeBytes,
        System.DateTimeOffset? DeletedAt,
        string? Sha256);

    private sealed class MutableLoggedDownload(string fileName, System.DateTimeOffset downloadedAt, long sizeBytes)
    {
        public string FileName { get; set; } = fileName;
        public System.DateTimeOffset DownloadedAt { get; } = downloadedAt;
        public long SizeBytes { get; set; } = sizeBytes;
        public System.DateTimeOffset? DeletedAt { get; set; }
        public string? Sha256 { get; set; }
    }
}
