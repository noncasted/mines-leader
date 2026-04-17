using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using Internal;
using Shared;
using UnityEngine;

namespace Menu.Screens.Cards.Preview.Sync
{
    /// <summary>
    /// Dispatches <see cref="ICardActionData"/> to the matching <see cref="ICardActionSync"/> resolver
    /// registered in menu scope. Reuses the exact gameplay action sync classes (so ZipZap lightning,
    /// Smoke fog, Blackout overlay etc. render the same way as during a real match).
    ///
    /// The resolver is the nested <c>Resolver&lt;TImpl, TData&gt;</c> inside
    /// <see cref="CardActionExtensions"/>. We inspect its generic argument at setup time to build
    /// <c>payload-type -&gt; sync</c> map, so dispatch is a single dictionary lookup.
    /// </summary>
    public sealed class MenuCardActionSyncRegistry : IScopeSetup
    {
        public MenuCardActionSyncRegistry(IReadOnlyList<ICardActionSync> resolvers)
        {
            _resolvers = resolvers;
        }

        private readonly IReadOnlyList<ICardActionSync> _resolvers;
        private readonly Dictionary<Type, ICardActionSync> _byPayload = new();

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            foreach (var sync in _resolvers)
            {
                var payloadType = ExtractPayloadType(sync);

                if (payloadType == null)
                    continue;

                _byPayload[payloadType] = sync;
            }

            Debug.Log($"[Preview] SyncRegistry built: {_byPayload.Count} payload-type mappings.");
        }

        public async UniTask Dispatch(IReadOnlyLifetime lifetime, ICardActionData data)
        {
            if (data == null)
                return;

            if (_byPayload.TryGetValue(data.GetType(), out var sync) == false)
            {
                Debug.LogWarning($"[Preview] SyncRegistry.Dispatch: no sync registered for {data.GetType().Name}.");
                return;
            }

            try
            {
                await sync.Sync(lifetime, data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Preview] SyncRegistry.Dispatch({data.GetType().Name}): {ex}");
            }
        }

        private static Type ExtractPayloadType(ICardActionSync sync)
        {
            // Resolvers registered via AddCardActionSyncResolver<TImpl, TData> are the nested
            // CardActionExtensions.Resolver<,> — TData (second generic arg) is the payload type.
            var type = sync.GetType();

            if (type.IsGenericType == false)
                return null;

            var args = type.GetGenericArguments();

            if (args.Length != 2)
                return null;

            return args[1];
        }
    }
}
