---
task: menu_progression_rework
updated: 2026-07-05
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| [1] Создать доменную модель и интерфейс `IProgression` в `Meta/Progression` | [x] | `dotnet build client/Meta.csproj` | — |
| [2] Реализовать сервис `Progression` и зарегистрировать его в DI | [x] | `dotnet build client/Meta.csproj` | — |
| [3] Переделать UI-элемент `ProgressionMilestone.cs` под спрайты статусов | [x] | `dotnet build client/Menu.csproj` | — |
| [4] Реворк `MenuProgression.cs` — только view | [x] | `dotnet build client/Menu.csproj`; grep — нет `IBackendProjection`/`ILootProgressionConfigs`/`IMetaBackend` | — |
| [5] Проверка сборки и базовых сценариев | [x] | `dotnet build client/Meta.csproj`, `dotnet build client/Menu.csproj` | — |

## Заметки
<!-- Сюда записываются находки, решения и полезная информация по ходу реализации -->

### [2026-07-05 14:00] Завершены модель, сервис и UI
- Созданы `IProgression`, `ProgressionMilestone`, `ProgressionMilestoneStatus`, `ProgressionService`.
- Сервис `ProgressionService` подписывается на `ProgressionProjection`, `LootProjection`, `LootProgressionOptions` и строит реактивный `ViewableList<IProgressionMilestone>`.
- `ProgressionMilestone` (UI) теперь слушает `Status` и `BoxId`, переключает `_taken`/`_active`/`_locked` и блокирует кнопку при отсутствии лутбокса.
- `MenuProgression` больше не зависит от бэкенд-проекций/конфигов — только от `IProgression`.
- Для избежания конфликта имён между namespace `Meta.Progression` и классом сервис класс назван `ProgressionService` (файл `Progression.cs`).
- Добавлена модель `BoxId` в `IProgressionMilestone` (необходимо для корректного открытия лутбокса и блокировки кнопки); отклонение от исходного описания зафиксировано в `_result.md`.

### [2026-07-05 13:45] Исправления сборки
- `Progression.cs`: `using Meta.Cards;` → `using Meta;` (namespace `Meta`, а не `Meta.Cards`).
- `IProgression.cs`: добавлен `using Shared;` для `CardType`.
- Переименован класс `Progression` → `ProgressionService` и конструктор, чтобы `MetaServicesExtensions` не путал тип с namespace `Meta.Progression`.
- `LootOpenResponse` содержит поле `LootBoxId`, а не `BoxId` — исправлено в `ProgressionService.OpenLootBox`.
