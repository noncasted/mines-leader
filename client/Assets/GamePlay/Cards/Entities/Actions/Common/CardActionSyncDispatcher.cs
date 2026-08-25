using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public interface ICardActionSyncDispatcher
    {
        UniTask Dispatch(IReadOnlyLifetime lifetime, ICardActionData data);
    }

    /// <summary>
    /// Maps <see cref="ICardActionData"/> runtime types to the matching
    /// <see cref="ICardActionSync"/> resolver. Used for menu previews and for
    /// nested copied actions (MirrorMatch).
    /// </summary>
    public sealed class CardActionSyncDispatcher : ICardActionSyncDispatcher, IScopeSetup
    {
        public CardActionSyncDispatcher(IReadOnlyList<ICardActionSync> resolvers)
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
        }

        public async UniTask Dispatch(IReadOnlyLifetime lifetime, ICardActionData data)
        {
            if (_byPayload.TryGetValue(data.GetType(), out var sync) == true)
                await sync.Sync(lifetime, data);

            if (data is CardActionSnapshot.MirrorMatch mirrorMatch &&
                mirrorMatch.CopiedAction != null)
                await Dispatch(lifetime, mirrorMatch.CopiedAction);
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
