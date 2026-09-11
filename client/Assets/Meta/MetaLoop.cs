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
            IBackendProjectionsAwaiter projections,
            IBackendProjection<SharedBackendUser.ProfileProjection> profile)
        {
            _authentication = authentication;
            _backend = backend;
            _projections = projections;
            _profile = profile;
        }

        private readonly IAuthentication _authentication;
        private readonly IMetaBackend _backend;
        private readonly IBackendProjectionsAwaiter _projections;
        private readonly IBackendProjection<SharedBackendUser.ProfileProjection> _profile;

        public UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime)
        {
            // Авторизация и проекции сетап не держат: реестры меты и меню грузятся параллельно,
            // а готовности данных меню дожидается само (см. MenuLoop).
            Connect(lifetime).Forget();

            return UniTask.CompletedTask;
        }

        private async UniTask Connect(IReadOnlyLifetime lifetime)
        {
            Debug.Log("[Meta] [Loop] Starting meta loop initialization");

            // Ветка идёт параллельно основной загрузке, поэтому её отрезок лежит в корне трассы
            // и на стек не встаёт: иначе этапы меню вложились бы в ожидание сокета. Текущим он
            // делается только на синхронных кусках, где вложенные замеры успевают его забрать.
            using var stage = GameProfiler.Detached("Meta connection");

            // Авторизация уехала в query запроса на апгрейд сокета: отдельного http-запроса
            // и отдельного кадра с хендшейком на старте больше нет.
            var savedUserId = _authentication.Load();
            var connectionLifetime = lifetime.Child();

            // Подписка на готовность — до коннекта: MenuLoop ждёт того же флага и закрывает трассу,
            // а продолжения идут в порядке подписки. Так ветка меты успевает закрыть свои отрезки.
            var projectionsTask = _projections.IsInitialized.WaitTrue(lifetime);

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
                return;
            }

            if (profile.Id != savedUserId)
            {
                using (GameProfiler.Ambient(stage))
                    _authentication.Save(profile.Id);
            }

            Debug.Log("[Meta] [Loop] Meta loop initialization completed successfully: " + profile.Id);
        }
    }
}
