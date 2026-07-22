namespace VolturaDownloadWatcher.Tests;

public sealed class MonitoringSessionTests
{
    [Fact]
    public void Advance_InvalidatesCallbacksFromThePreviousSession()
    {
        var sessions = new MonitoringSession();
        var previousSession = sessions.Current;

        var currentSession = sessions.Advance();

        Assert.False(sessions.IsCurrent(previousSession));
        Assert.True(sessions.IsCurrent(currentSession));
    }

    [Fact]
    public void EveryTransitionGetsANewSession()
    {
        var sessions = new MonitoringSession();

        var pausedSession = sessions.Advance();
        var resumedSession = sessions.Advance();

        Assert.NotEqual(pausedSession, resumedSession);
        Assert.False(sessions.IsCurrent(pausedSession));
        Assert.True(sessions.IsCurrent(resumedSession));
    }
}
