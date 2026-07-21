namespace VolturaDownloadWatcher;

internal static class ReleaseUpdateChecker
{
    internal const string ProductPageUrl = "https://voltura.github.io/voltura-download-watcher/";
    internal const string LatestReleasePageUrl = "https://github.com/voltura/voltura-download-watcher/releases/latest";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/voltura/voltura-download-watcher/releases/latest";
    private static readonly System.Net.Http.HttpClient Client = CreateClient();

    internal static async System.Threading.Tasks.Task<ReleaseCheckResult> CheckAsync(
        string runningVersion,
        System.Threading.CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Client.GetAsync(LatestReleaseApiUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            var releaseUrl = root.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : null;
            var latestVersion = NormalizeVersionText(tag);
            if (!TryParseVersion(latestVersion, out _))
            {
                return ReleaseCheckResult.Failed("GitHub returned an unrecognized release version.");
            }

            return new ReleaseCheckResult(
                true,
                IsNewer(latestVersion, runningVersion),
                latestVersion,
                string.IsNullOrWhiteSpace(releaseUrl) ? LatestReleasePageUrl : releaseUrl,
                null);
        }
        catch (System.Exception ex) when (ex is not System.OperationCanceledException)
        {
            return ReleaseCheckResult.Failed($"{ex.GetType().FullName}: {ex.Message}");
        }
    }

    internal static bool IsNewer(string? candidateVersion, string runningVersion) =>
        TryParseVersion(candidateVersion, out var candidate)
        && TryParseVersion(runningVersion, out var running)
        && candidate > running;

    internal static string NormalizeVersionText(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        return suffixIndex >= 0 ? normalized[..suffixIndex] : normalized;
    }

    private static bool TryParseVersion(string? value, out System.Version version)
    {
        var normalized = NormalizeVersionText(value);
        var components = normalized.Split('.', System.StringSplitOptions.RemoveEmptyEntries);
        if (components.Length is < 1 or > 4
            || components.Any(component => !int.TryParse(component, out _)))
        {
            version = new System.Version();
            return false;
        }

        var padded = string.Join('.', components.Concat(System.Linq.Enumerable.Repeat("0", 4 - components.Length)));
        return System.Version.TryParse(padded, out version!);
    }

    private static System.Net.Http.HttpClient CreateClient()
    {
        var client = new System.Net.Http.HttpClient
        {
            Timeout = System.TimeSpan.FromSeconds(8)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VolturaDownloadWatcher/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }
}

internal sealed record ReleaseCheckResult(
    bool Success,
    bool UpdateAvailable,
    string? LatestVersion,
    string? ReleaseUrl,
    string? Error)
{
    internal static ReleaseCheckResult Failed(string error) => new(false, false, null, null, error);
}
