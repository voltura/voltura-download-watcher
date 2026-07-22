namespace VolturaDownloadWatcher;

internal static class FilenameMarqueeTimeline
{
    private const double PixelsPerSecond = 72;
    private static readonly System.TimeSpan EdgePause = System.TimeSpan.FromSeconds(2);

    public static KeyTimes Create(double overflowPixels)
    {
        var travel = System.TimeSpan.FromSeconds(overflowPixels / PixelsPerSecond);
        var forwardEnd = EdgePause + travel;
        var farEdgePauseEnd = forwardEnd + EdgePause;
        var returnEnd = farEdgePauseEnd + travel;
        return new KeyTimes(EdgePause, forwardEnd, farEdgePauseEnd, returnEnd, returnEnd + EdgePause);
    }

    internal readonly record struct KeyTimes(
        System.TimeSpan StartPauseEnd,
        System.TimeSpan ForwardEnd,
        System.TimeSpan FarEdgePauseEnd,
        System.TimeSpan ReturnEnd,
        System.TimeSpan CycleEnd);
}
