namespace Shared
{
    public static class SharedBackendSocketAuth
    {
        /// <summary>
        /// Id юзера уезжает в query запроса на апгрейд сокета: отдельного кадра с
        /// авторизацией нет, поэтому старт не тратит лишнее плечо до сервера.
        /// Пустое значение означает, что сохранённого юзера у клиента нет и сервер
        /// заведёт нового — его id приедет в профильной проекции.
        /// </summary>
        public const string UserIdQueryKey = "userId";
    }
}
