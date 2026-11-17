namespace Common.Network;

public readonly record struct SessionTicket(Guid UserId, Guid SessionId, DateTime ExpiresAt, Guid Nonce);
