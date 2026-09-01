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
            IMetaConnectionAwaiter connectionAwaiter,
            IBackendProjection<SharedBackendUser.ProfileProjection> profile)
        {
            _authentication = authentication;
            _backend = backend;
            _connectionAwaiter = connectionAwaiter;
            _profile = profile;
        }

        private readonly IAuthentication _authentication;
        private readonly IMetaBackend _backend;
        private readonly IMetaConnectionAwaiter _connectionAwaiter;
        private readonly IBackendProjection<SharedBackendUser.ProfileProjection> _profile;

        public async UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime)
        {
            Debug.Log("[Meta] [Loop] Starting meta loop initialization");

            // Отрезок на этот слушатель открыт снаружи, в EventLoop: этапы скоупа стартуют
            // пачкой, и по стеку такая вложенность не построилась бы.
            var stage = GameProfiler.CurrentScope;

            // Авторизация уехала в query запроса на апгрейд сокета: отдельного http-запроса
            // и отдельного кадра с хендшейком на старте больше нет.
            var savedUserId = _authentication.Load();
            var connectionLifetime = lifetime.Child();

            await stage.MeasureNested("Connect", () => _backend.Connect(connectionLifetime, savedUserId));

            Debug.Log("[Meta] [Loop] Waiting for connection completion");

            // Проекции приезжают с бэкенда пачкой после коннекта: этот отрезок и есть
            // ожидание данных, без которых меню открывать нечем.
            await stage.Measure("Projections", () => _connectionAwaiter.CompleteTask);

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
                _authentication.Save(profile.Id);

            Debug.Log("[Meta] [Loop] Meta loop initialization completed successfully: " + profile.Id);
        }
    }
}
