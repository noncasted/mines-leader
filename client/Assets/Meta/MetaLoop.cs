using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.Telemetry;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    // The first connect of the meta socket (retried until it succeeds) and the registries of the meta; the reconnects
    // after it are MetaReconnection.
    public class MetaLoop : IScopeSetup
    {
        public MetaLoop(
            IAuthentication authentication,
            IMetaBackend backend,
            IMetaState state,
            IStartupAssetsPreload startupAssets,
            IReadOnlyList<IMetaRegistry> registries,
            IBackendProjection<SharedBackendUser.ProfileProjection> profile,
            IClientOutdatedNotice outdatedNotice,
            IClientTelemetry telemetry,
            IMetaReconnection reconnection,
            ILog<MetaLoop> log)
        {
            _log = log;
            _authentication = authentication;
            _backend = backend;
            _state = state;
            _startupAssets = startupAssets;
            _registries = registries;
            _profile = profile;
            _outdatedNotice = outdatedNotice;
            _telemetry = telemetry;
            _reconnection = reconnection;
        }

        // A failed first connect is tried again after this pause (the server may be restarting).
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

        private readonly ILog _log;
        private readonly IAuthentication _authentication;
        private readonly IMetaBackend _backend;
        private readonly IMetaState _state;
        private readonly IStartupAssetsPreload _startupAssets;
        private readonly IReadOnlyList<IMetaRegistry> _registries;
        private readonly IBackendProjection<SharedBackendUser.ProfileProjection> _profile;
        private readonly IClientOutdatedNotice _outdatedNotice;
        private readonly IClientTelemetry _telemetry;
        private readonly IMetaReconnection _reconnection;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            // Подключение и реестры сетап не держат: меню грузится параллельно с ними,
            // а готовности меты дожидается само (см. MenuLoop).
            Connect(lifetime).Forget();
            LoadRegistries(lifetime).Forget();
        }

        private async UniTask Connect(IReadOnlyLifetime lifetime)
        {
            // The server may be down or restarting at the start: the first connect is retried until it succeeds,
            // the menu waits on its loading screen meanwhile.
            while (true)
            {
                try
                {
                    var isAuthorized = await Authorize(lifetime);

                    // Флаги ветки ставятся после закрытия её отрезков: за последним из них синхронно
                    // открывается меню и закрывает всю трассу.
                    if (isAuthorized == true)
                    {
                        // Before the flag: the first session of the telemetry (GameLoop) opens on it and refers to this record.
                        _telemetry.SendStartupTrace();
                        _state.SetAuthorized();
                    }

                    return;
                }
                // The saved guest is refused (a wrong secret, a user from before the secret): every retry is refused
                // the same way. The menu stays on its loading screen; the record is kept for the investigation (in the
                // editor: Project Tools > User).
                catch (MetaAuthRejectedException exception) when (lifetime.IsTerminated == false)
                {
                    _log.Error("The saved guest is refused, the meta connection is not retried: {Status}", exception.Status);
                    return;
                }
                // The explicit retry: whatever fails here (no network, the socket lost, no projections) is tried
                // again. Only the end of the meta scope stops it.
                catch (Exception exception) when (lifetime.IsTerminated == false)
                {
                    // The server closed the socket as one of an outdated client: no retry can succeed, the notice
                    // with the way to update covers the menu.
                    if (_outdatedNotice.IsShown == true)
                    {
                        _log.Warning("The client is outdated: the meta connection is not retried");
                        return;
                    }

                    _log.Warning("Meta connection failed, next attempt in {Seconds} s: {Type} {Message}",
                        RetryDelay.TotalSeconds, exception.GetType().Name, exception.Message);
                }

                await UniTask.Delay(RetryDelay, DelayType.Realtime, cancellationToken: lifetime.Token);
            }
        }

        private async UniTask<bool> Authorize(IReadOnlyLifetime lifetime)
        {
            _log.Info("Starting meta connection");

            // The branch runs in parallel with the whole start: a root of the trace of its own (not under the Meta stage) and
            // off the stack, or the stages of the menu would nest into the wait for the socket. It is made current only on the
            // synchronous parts, where the nested records take it.
            using var stage = GameProfiler.Background("Meta connection");

            // The credentials are the first frame of the socket (S3): the saved guest of this backend environment
            // or nothing (a new guest).
            var auth = MetaSocketAuth.CreateRequest(_authentication, _log);
            var connectionLifetime = lifetime.Child();
            Guid userId;

            // A connection that did not get its projections is not alive: a failed attempt ends it.
            try
            {
                // Подписка на проекции — до коннекта, чтобы отрезок ожидания не пропустил их приезд.
                var projectionsTask = _state.IsProjectionsReceived.WaitTrue(lifetime);
                var loss = new MetaConnectionLoss(_backend.Connection, connectionLifetime);

                var connect = stage.Child("Connect");
                connect.Start();

                UniTask<SharedBackendSocketAuth.Response> connectTask;

                using (GameProfiler.Ambient(connect))
                    connectTask = _backend.Connect(connectionLifetime, auth);

                var response = await connect.Track(connectTask);

                // The secret is issued once (a new guest, or a saved guest the server does not know - a wiped
                // database): saved before anything else.
                if (string.IsNullOrEmpty(response.IssuedSecret) == false)
                {
                    using (GameProfiler.Ambient(stage))
                        _authentication.SaveGuest(new GuestCredentials(response.UserId, response.IssuedSecret));
                }

                userId = response.UserId;

                _log.Info("Waiting for projections");

                // Проекции приезжают с бэкенда пачкой после коннекта: этот отрезок и есть
                // ожидание данных, без которых меню открывать нечем.
                await stage.Measure("Projections", loss.Race(projectionsTask));
            }
            catch
            {
                connectionLifetime.Terminate();
                throw;
            }

            var profile = _profile.Value;

            if (profile == null)
            {
                connectionLifetime.Terminate();
                _log.Error("Profile projection is missing, user is not initialized");
                return false;
            }

            if (profile.Id != userId)
            {
                connectionLifetime.Terminate();
                throw new InvalidOperationException($"[Meta] [Loop] The profile {profile.Id} is not the user {userId} of the connection");
            }

            _reconnection.OnConnected(lifetime, connectionLifetime, userId);

            _log.Info("Meta connection completed successfully: {UserId}", profile.Id);
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
