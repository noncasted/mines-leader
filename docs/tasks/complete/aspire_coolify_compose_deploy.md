## Aspire Coolify Compose Deploy

### Что сделано
- Переведен прод-деплой с DinD/privileged (`aspire run` внутри одного контейнера ~2.5 GB) на Coolify Docker Compose с per-service Release-контейнерами (~734 MiB idle, −70%).
- Создан единый multi-stage Dockerfile с `COPY --parents` и `publish-all` stage; 6 runtime targets через `ASSEMBLY_NAME` build-arg.
- Compose-стек из 9 сервисов (pgbouncer, migrator, silo, coordinator, meta, game, console, aspire-dashboard, resource-service) поднят на https://<svc>.minesleader.xyz с Let's Encrypt TLS.
- Решена сетевая проблема с managed Postgres: `coolify` shared network через `external: true`.
- Добавлен `DeploySetup` init-container с embedded Orleans clustering schema bootstrap + supplemental patch.
- Добавлены `OrleansReadyHealthCheck` и `CoordinatorReadyHealthCheck`; `MapDefaultEndpoints` теперь работает во всех окружениях.
- Форк `Aspire.ResourceServer.Standalone` допатчен: compose label filter, state mapping, display-name/exit-code.
- Исправлен Blazor `_framework/blazor.web.js` 404, Console `AntiforgeryValidationException`, dashboard `CryptographicException` (persistent volumes + `user: root`).
- Созданы `DEPLOY.md` и `DEPLOY_TROUBLESHOOTING.md` (8 разделов).
- Удалена старая DinD-инфраструктура.

### Ключевые файлы
| Файл | Роль |
|------|------|
| `backend/Tools/deploy/docker-compose.yaml` | Compose-стек для Coolify |
| `backend/Orchestration/Dockerfile` | Multi-stage shared build |
| `backend/Tools/DeploySetup/Program.cs` | Init-container миграций |
| `backend/Tools/DeploySetup/OrleansClusteringSetup.cs` | Schema bootstrap |
| `backend/Orchestration/Extensions/OrleansReadyHealthCheck.cs` | Orleans readiness |
| `backend/Orchestration/Extensions/CoordinatorReadyHealthCheck.cs` | Coordinator readiness |
| `docs/db/docs/DEPLOY.md` | Полное руководство |
| `docs/db/docs/DEPLOY_TROUBLESHOOTING.md` | Troubleshooting |

### Заметки
- `docker-compose.yaml` живет в `backend/Tools/deploy/`, не в корне — Coolify Base Directory настроен соответственно.
- Console: никогда не используй `UseStaticFiles()` вместе с `MapStaticAssets` в .NET 9+ — ломает framework asset routing.
- Blazor в контейнере: образ `dotnet/sdk` не auto-restore'ит `Microsoft.AspNetCore.App.Internal.Assets`; нужен explicit PackageReference.
- Traefik pool stale на healthy контейнере — известная проблема Coolify, лечится `docker restart coolify-proxy`.
- Незакрытые: cutover старого DinD (не проверено), PR в upstream resource-service (опционально).
