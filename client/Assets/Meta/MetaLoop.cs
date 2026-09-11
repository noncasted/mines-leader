using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    public class MetaLoop : IScopeBaseSetupAsync
    {
        public MetaLoop(
            IAuthentication authentication,
            IMetaBackend backend,
            IMetaState state,
            IStartupAssetsPreload startupAssets,
            IReadOnlyList<IMetaRegistry> registries,
            IBackendProjection<SharedBackendUser.ProfileProjection> profile)
        {
            _authentication = authentication;
            _backend = backend;
            _state = state;
            _startupAssets = startupAssets;
            _registries = registries;
            _profile = profile;
        }

        private readonly IAuthentication _authentication;
        private readonly IMetaBackend _backend;
        private readonly IMetaState _state;
        private readonly IStartupAssetsPreload _startupAssets;
        private readonly IReadOnlyList<IMetaRegistry> _registries;
        private readonly IBackendProjection<SharedBackendUser.ProfileProjection> _profile;

        public UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime)
        {
            // Подключение и реестры сетап не держат: меню грузится параллельно с ними,
            // а готовности меты дожидается само (см. MenuLoop).
            Connect(lifetime).Forget();
            LoadRegistries(lifetime).Forget();

            return UniTask.CompletedTask;
        }

        private async UniTask Connect(IReadOnlyLifetime lifetime)
        {
            var isAuthorized = await Authorize(lifetime);

            // Флаги ветки ставятся после закрытия её отрезков: за последним из них синхронно
            // открывается меню и закрывает всю трассу.
            if (isAuthorized == true)
                _state.SetAuthorized();
        }

        private async UniTask<bool> Authorize(IReadOnlyLifetime lifetime)
        {
            Debug.Log("[Meta] [Loop] Starting meta connection");

            // Ветка идёт параллельно основной загрузке, поэтому её отрезок лежит в корне трассы
            // и на стек не встаёт: иначе этапы меню вложились бы в ожидание сокета. Текущим он
            // делается только на синхронных кусках, где вложенные замеры успевают его забрать.
            using var stage = GameProfiler.Detached("Meta connection");

            // Авторизация уехала в query запроса на апгрейд сокета: отдельного http-запроса
            // и отдельного кадра с хендшейком на старте больше нет.
            var savedUserId = _authentication.Load();
            var connectionLifetime = lifetime.Child();

            // Подписка на проекции — до коннекта, чтобы отрезок ожидания не пропустил их приезд.
            var projectionsTask = _state.IsProjectionsReceived.WaitTrue(lifetime);

            var connect = stage.Child("Connect");
            connect.Start();

            UniTask connectTask;

            using (GameProfiler.Ambient(connect))
                connectTask = _backend.Connect(connectionLifetime, savedUserId);

            await connect.Track(connectTask);

            Debug.Log("[Meta] [Loop] Waiting for projections");

            // Проекции приезжают с бэкенда пачкой после коннекта: этот отрезок и есть
            // ожидание данных, без которых меню открывать нечем.
            await stage.Measure("Projections", projectionsTask);

            // Кто мы такие, говорит профильная проекция: сохранённого id могло не быть
            // вовсе, и тогда сервер завёл нового юзера прямо на коннекте.
            var profile = _profile.Value;

            if (profile == null)
            {
                connectionLifetime.Terminate();
                Debug.LogError("[Meta] [Loop] Profile projection is missing, user is not initialized");
                return false;
            }

            if (profile.Id != savedUserId)
            {
                using (GameProfiler.Ambient(stage))
                    _authentication.Save(profile.Id);
            }

            Debug.Log("[Meta] [Loop] Meta connection completed successfully: " + profile.Id);
            return true;
        }

        private async UniTask LoadRegistries(IReadOnlyLifetime lifetime)
        {
            using (var stage = GameProfiler.Detached("Meta registries"))
            {
                // Реестры раскладывают спрайты по определениям, а спрайты качаются со старта.
                await stage.Measure("Startup assets", _startupAssets.IsLoaded.WaitTrue(lifetime));

                using (GameProfiler.Ambient(stage))
                using (GameProfiler.Scope("Initialize"))
                {
                    foreach (var registry in _registries)
                        registry.Initialize();
                }
            }

            _state.SetRegistriesLoaded();
        }
    }
}
