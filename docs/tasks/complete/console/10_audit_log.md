## Задача: Audit Log — лог админских действий

### Цель
Создать систему логирования действий администратора в консоли: изменение конфигов, тоггл features, создание ботов, debug-команды. Сейчас все изменения происходят без следа.

### Контекст
Нет никакой инфраструктуры для аудита. Нужно создать с нуля: storage, запись, отображение. Все существующие операции (Config save, Feature toggle, Bot create) нужно инструментировать.

### Шаги реализации

**1. Создать AuditLog storage**
  1.1. Создать таблицу PostgreSQL: `audit_log` с полями: `id UUID`, `timestamp TIMESTAMPTZ`, `action TEXT`, `details JSONB`, `source TEXT`
  1.2. Создать `IAuditLogStorage` / `AuditLogStorage` — `backend/Infrastructure/Orleans/Audit/AuditLogStorage.cs` [новый файл]
  1.3. Методы: `Write(AuditEntry)`, `Read(int limit, int offset)`, `ReadByAction(string action, int limit)`

**2. Создать AuditEntry DTO**
  2.1. `AuditEntry`: `Guid Id`, `DateTime Timestamp`, `string Action` (ConfigSaved, FeatureToggled, BotCreated, etc.), `string Details` (JSON), `string Source` (Console, API)

**3. Инструментировать существующие операции**
  3.1. `Configs.razor` — при Save: записать какой конфиг изменился — `backend/Console/Pages/Configs/Configs.razor`
  3.2. `Features.razor` — при Toggle: записать какая feature и новое значение — `backend/Console/Pages/Features/Features.razor`
  3.3. `BotManagement.razor` — при Create: записать Id бота — `backend/Console/Pages/Bots/BotManagement.razor`
  3.4. Future: Debug Panel (#8) — записать команду и параметры

**4. Создать Blazor страницу**
  4.1. Создать `backend/Console/Pages/AuditLog/AuditLog.razor` [новый файл — добавить в Console.csproj]
  4.2. Route: `/audit`
  4.3. Таблица: Timestamp, Action, Details, Source
  4.4. Фильтр по типу действия
  4.5. Пагинация

**5. Добавить виджет "Recent Activity" на Home**
  5.1. Создать `backend/Console/Pages/Home/RecentActivityWidget.razor` [новый файл]
  5.2. Последние 5 действий в компактном виде

**6. Интеграция**
  6.1. Добавить роут в `ConsoleConstants.Pages`
  6.2. DI регистрация `IAuditLogStorage`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Orleans/Audit/AuditLogStorage.cs` | Новый storage [новый файл] |
| `backend/Console/Pages/Configs/Configs.razor` | Инструментировать Save |
| `backend/Console/Pages/Features/Features.razor` | Инструментировать Toggle |
| `backend/Console/Pages/Bots/BotManagement.razor` | Инструментировать Create |
| `backend/Console/Pages/AuditLog/AuditLog.razor` | Новая страница [новый файл] |
| `backend/Console/Pages/Home/RecentActivityWidget.razor` | Новый виджет [новый файл] |

### Документация к прочтению
- `rules/BLAZOR.md` — UiComponent, таблицы
- `rules/CODE_STYLE.md` — member order, naming

### Риски
- **Объем инструментирования**: нужно обернуть каждую операцию в console. Можно начать с configs/features и расширять
- **PostgreSQL setup**: нужна миграция для создания таблицы. Добавить в `StatesSetup` или отдельный скрипт
- **Нет auth**: сейчас Console без аутентификации — поле "source" не может идентифицировать пользователя. На будущее
