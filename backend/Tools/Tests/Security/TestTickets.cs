using Common.Network;

namespace Tests.Security;

// Session tickets over a fixed key and a manual clock (S4).
public static class TestTickets
{
    public static readonly SessionTicketKey Key = new(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    public static SessionTickets Create(TimeProvider time)
    {
        return new SessionTickets(Key, time);
    }

    public static SessionTickets Create()
    {
        return Create(TimeProvider.System);
    }
}
