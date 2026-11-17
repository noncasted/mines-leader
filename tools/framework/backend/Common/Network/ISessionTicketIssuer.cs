namespace Common.Network;

public interface ISessionTicketIssuer
{
    string Issue(Guid userId, Guid sessionId);
}
