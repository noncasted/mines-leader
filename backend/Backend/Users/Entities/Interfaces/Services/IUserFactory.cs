namespace Backend.Users;

public interface IUserFactory
{
    Task<Guid> Create(UserCreateOptions options);
}