using Cysharp.Threading.Tasks;

namespace Internal
{
    /// <summary>
    /// Контракт по потокам: <see cref="Received"/> и <see cref="Closed"/> вызываются на главном
    /// потоке, а <see cref="Connect"/> и <see cref="Send"/> возвращают управление на главный поток.
    /// Выше по стеку (reader, writer, команды) никто про потоки не думает.
    /// </summary>
    public interface IWebSocket
    {
        IViewableDelegate<byte[]> Received { get; }

        /// <summary>
        /// Соединение оборвалось не по нашей инициативе: закрыл сервер, упала сеть, ошибка
        /// сокета. Вызывается один раз с причиной для лога. При локальном завершении
        /// лайфтайма не вызывается.
        /// </summary>
        IViewableDelegate<string> Closed { get; }

        UniTask Connect();
        UniTask Send(byte[] bytes);
    }
}