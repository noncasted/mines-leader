namespace Meta.Users;

/// <summary>
/// Читает все проекции юзера в текущей транзакции. Список источников собирает генератор,
/// который к Meta не подключён, поэтому реализация регистрируется снаружи.
/// </summary>
public interface IUserProjectionsLoader
{
    Task<IReadOnlyList<IProjectionPayload>> Load(Guid userId);
}
