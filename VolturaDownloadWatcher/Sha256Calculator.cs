namespace VolturaDownloadWatcher;

internal static class Sha256Calculator
{
    internal static async System.Threading.Tasks.Task<Sha256CalculationResult> CalculateStableAsync(
        string path,
        System.Threading.CancellationToken cancellationToken)
    {
        const int maximumAttempts = 8;
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var before = GetSnapshot(path);
                await System.Threading.Tasks.Task.Delay(500, cancellationToken).ConfigureAwait(false);
                var settled = GetSnapshot(path);
                if (before != settled)
                {
                    continue;
                }

                await using var stream = new System.IO.FileStream(
                    path,
                    System.IO.FileMode.Open,
                    System.IO.FileAccess.Read,
                    System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete,
                    128 * 1024,
                    System.IO.FileOptions.Asynchronous | System.IO.FileOptions.SequentialScan);
                var digest = await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken)
                    .ConfigureAwait(false);
                var after = GetSnapshot(path);
                if (settled == after)
                {
                    return new Sha256CalculationResult(
                        System.Convert.ToHexString(digest),
                        after.Length,
                        null);
                }
            }
            catch (System.IO.FileNotFoundException ex)
            {
                return new Sha256CalculationResult(null, 0, ex.Message);
            }
            catch (System.IO.DirectoryNotFoundException ex)
            {
                return new Sha256CalculationResult(null, 0, ex.Message);
            }
            catch (System.IO.IOException) when (attempt < maximumAttempts - 1)
            {
            }
            catch (System.UnauthorizedAccessException) when (attempt < maximumAttempts - 1)
            {
            }

            await System.Threading.Tasks.Task.Delay(350, cancellationToken).ConfigureAwait(false);
        }

        return new Sha256CalculationResult(null, 0, "File did not become stable or readable.");
    }

    private static FileSnapshot GetSnapshot(string path)
    {
        var file = new System.IO.FileInfo(path);
        file.Refresh();
        if (!file.Exists)
        {
            throw new System.IO.FileNotFoundException("File no longer exists.", path);
        }

        return new FileSnapshot(file.Length, file.LastWriteTimeUtc);
    }

    private readonly record struct FileSnapshot(long Length, System.DateTime LastWriteTimeUtc);
}

internal sealed record Sha256CalculationResult(string? Hash, long SizeBytes, string? Error)
{
    internal bool Success => !string.IsNullOrWhiteSpace(Hash);
}
