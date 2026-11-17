using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Common.Network;

// F1.1: the entry into a game session. The meta issues a ticket to the user it has authenticated on the meta
// socket; the game gateway takes the user and the session only from a ticket it can verify.
// Format: v1.<base64url(payload)>.<base64url(mac)>, payload (56 bytes) = UserId (16) + SessionId (16) +
// ExpiresAt (unix seconds, int64 little endian) + Nonce (16); mac = HMAC-SHA256(secret, "v1." + payload).
// The client does not parse it.
public class SessionTickets : ISessionTicketIssuer, ISessionTicketVerifier
{
    public SessionTickets(SessionTicketKey key, TimeProvider time)
    {
        _key = key;
        _time = time;
    }

    // A reservation lives as long as the join timeout of its session (30 s): the ticket outlives it, so its expiry
    // never refuses an entry the reservation would allow.
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private const string Version = "v1";
    private const int PayloadSize = 16 + 16 + 8 + 16;
    private const int MacSize = 32;

    private static readonly byte[] VersionPrefix = Encoding.ASCII.GetBytes(Version + ".");

    private readonly SessionTicketKey _key;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, DateTime> _usedNonces = new();
    private readonly Lock _pruneLock = new();

    private DateTime _nextPrune = DateTime.MinValue;

    public string Issue(Guid userId, Guid sessionId)
    {
        var expiresAt = _time.GetUtcNow().UtcDateTime + Lifetime;
        var ticket = new SessionTicket(userId, sessionId, expiresAt, Guid.NewGuid());
        return Write(ticket);
    }

    public SessionTicketResult Verify(string ticket)
    {
        if (TryRead(ticket, out var parsed) == false)
            return new SessionTicketResult(SessionTicketCheck.Invalid, default);

        var now = _time.GetUtcNow().UtcDateTime;

        if (parsed.ExpiresAt <= now)
            return new SessionTicketResult(SessionTicketCheck.Expired, parsed);

        PruneUsedNonces(now);

        if (_usedNonces.TryAdd(parsed.Nonce, parsed.ExpiresAt) == false)
            return new SessionTicketResult(SessionTicketCheck.Replayed, parsed);

        return new SessionTicketResult(SessionTicketCheck.Ok, parsed);
    }

    private string Write(SessionTicket ticket)
    {
        Span<byte> payload = stackalloc byte[PayloadSize];

        ticket.UserId.TryWriteBytes(payload[..16]);
        ticket.SessionId.TryWriteBytes(payload.Slice(16, 16));
        BinaryPrimitives.WriteInt64LittleEndian(payload.Slice(32, 8), new DateTimeOffset(ticket.ExpiresAt).ToUnixTimeSeconds());
        ticket.Nonce.TryWriteBytes(payload.Slice(40, 16));

        Span<byte> mac = stackalloc byte[MacSize];
        ComputeMac(payload, mac);

        return $"{Version}.{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(mac)}";
    }

    private bool TryRead(string text, out SessionTicket ticket)
    {
        ticket = default;

        if (string.IsNullOrEmpty(text) == true)
            return false;

        var parts = text.Split('.');

        if (parts.Length != 3 || parts[0] != Version)
            return false;

        Span<byte> payload = stackalloc byte[PayloadSize];
        Span<byte> mac = stackalloc byte[MacSize];

        if (TryDecode(parts[1], payload) == false || TryDecode(parts[2], mac) == false)
            return false;

        Span<byte> expected = stackalloc byte[MacSize];
        ComputeMac(payload, expected);

        if (CryptographicOperations.FixedTimeEquals(mac, expected) == false)
            return false;

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64LittleEndian(payload.Slice(32, 8)));

        ticket = new SessionTicket(
            new Guid(payload[..16]),
            new Guid(payload.Slice(16, 16)),
            expiresAt.UtcDateTime,
            new Guid(payload.Slice(40, 16)));

        return true;
    }

    private static bool TryDecode(string text, Span<byte> destination)
    {
        if (Base64Url.IsValid(text, out var length) == false || length != destination.Length)
            return false;

        return Base64Url.TryDecodeFromChars(text, destination, out var written) == true && written == destination.Length;
    }

    private void ComputeMac(ReadOnlySpan<byte> payload, Span<byte> destination)
    {
        Span<byte> data = stackalloc byte[VersionPrefix.Length + PayloadSize];
        VersionPrefix.CopyTo(data);
        payload.CopyTo(data[VersionPrefix.Length..]);

        HMACSHA256.HashData(_key.Value, data, destination);
    }

    // Used nonces are kept until their tickets expire; an expired ticket is refused before its nonce is looked at.
    private void PruneUsedNonces(DateTime now)
    {
        lock (_pruneLock)
        {
            if (now < _nextPrune)
                return;

            _nextPrune = now + Lifetime;
        }

        foreach (var (nonce, expiresAt) in _usedNonces)
        {
            if (expiresAt <= now)
                _usedNonces.TryRemove(nonce, out _);
        }
    }
}
