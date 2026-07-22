namespace VolturaDownloadWatcher;

internal sealed class MonitoringSession
{
    private long _current;

    public long Current => System.Threading.Interlocked.Read(ref _current);

    public long Advance() => System.Threading.Interlocked.Increment(ref _current);

    public bool IsCurrent(long session) => session == Current;
}
