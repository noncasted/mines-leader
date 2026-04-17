## Board Actions Refactor

### Что сделано
- Убрана зависимость клиентской доски от `INetworkEntity` и `INetworkConnection` — `Board` больше не инжектит сетевые типы.
- `IsMine` теперь пробрасывается параметром в `IBoard.Setup(bool isMine)` из `GamePlayerFactory` (через `data.Owner.IsLocal`), а не вычисляется из entity.
- Введён интерфейс `IBoardActions` с методами `Flag/Unflag/Open/OpenMultiple(Vector2Int)` — единая точка отправки позиционных команд.
- Реализация `BoardActions` — единственный клиентский класс, знающий про `INetworkConnection` и `SharedGameAction.*` (SetFlag/RemoveFlag/Open/OpenMultiple).
- Все потребители (`CellTakenState`, `CellMultipleOpenAction`, `CellView`) переведены на `IBoardActions` вместо прямых вызовов `_connection.Request(new SharedGameAction...)`.

### Ключевые файлы
- `client/Assets/GamePlay/Boards/Root/IBoardActions.cs` — новый интерфейс
- `client/Assets/GamePlay/Boards/Root/BoardActions.cs` — новая реализация
- `client/Assets/GamePlay/Boards/Root/Board.cs` — новые зависимости, новый `Setup(bool)`
- `client/Assets/GamePlay/Boards/Cells/Taken/CellTakenState.cs` — переведён на `IBoardActions`
- `client/Assets/GamePlay/Players/Entity/PlayerEntityExtensions.cs` — регистрация `BoardActions` как `IBoardActions` в scope игрока
- `client/Assets/GamePlay/Players/Services/Factory/GamePlayerFactory.cs` — новый вызов `board.Setup(data.Owner.IsLocal)`

### Заметки
- `BoardActions` регистрируется в player entity scope (`AddPlayerComponents`), а не в корневом — каждый игрок получает свой экземпляр, хотя инстанс stateless и зависит только от `INetworkConnection`.
- `Setup` теперь имеет сигнатуру `Setup(bool isMine)` вместо `Setup(INetworkEntity)` — в интерфейсе `IBoard` тоже.
