using Game.ClientTelemetry;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

public class ClientTelemetryStorageOptionsTests
{
    // The close of the socket ends a session by itself; the timer stays for a gateway gone without closing its sockets. A page in the
    // background sends rarely, and it must not lose a live session to this timer.
    [Fact]
    public void LostAfter_ByDefault_IsTheSafetyTimerOfTwoMinutes()
    {
        Assert.Equal(TimeSpan.FromSeconds(120), new ClientTelemetryStorageOptions().LostAfter);
    }
}
