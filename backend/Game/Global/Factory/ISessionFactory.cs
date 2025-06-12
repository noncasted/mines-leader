namespace Game;

public interface ISessionFactory
{
    Task<Guid> Create(SessionCreateOptions createOptions);
}