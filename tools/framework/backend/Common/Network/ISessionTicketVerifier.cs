namespace Common.Network;

public interface ISessionTicketVerifier
{
    // Signature -> expiry -> nonce: a valid ticket is accepted once (the nonce is remembered until the ticket
    // expires). A session lives on one game server, so the set of used nonces is local to the process.
    SessionTicketResult Verify(string ticket);
}
