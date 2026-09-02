using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using VContainer;

namespace GamePlay.Cards
{
    public interface ICardActionSyncDispatcher
    {
        UniTask Dispatch(IReadOnlyLifetime lifetime, ICardActionData data);
    }

    /// <summary>
    /// Maps <see cref="ICardActionData"/> runtime types to the matching
    /// <see cref="ICardActionSync"/> resolver. Used for menu previews and for
    /// cards whose sync plays a nested action (MirrorMatch).
    /// </summary>
    public sealed class CardActionSyncDispatcher : ICardActionSyncDispatcher, IScopeSetup
    {
        public CardActionSyncDispatcher(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        private readonly IObjectResolver _resolver;
        private readonly Dictionary<Type, ICardActionSync> _byPayload = new();

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            // Resolvers are pulled here rather than injected through the constructor:
            // MirrorMatch's resolver depends back on this dispatcher to play its copied action.
            foreach (var sync in _resolver.Resolve<IReadOnlyList<ICardActionSync>>())
            {
                var payloadType = ExtractPayloadType(sync);

                if (payloadType == null)
                    continue;

                _byPayload[payloadType] = sync;
            }
        }

        public async UniTask Dispatch(IReadOnlyLifetime lifetime, ICardActionData data)
        {
            if (_byPayload.TryGetValue(data.GetType(), out var sync) == true)
                await sync.Sync(lifetime, data);
        }

        private static Type ExtractPayloadType(ICardActionSync sync)
        {
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
