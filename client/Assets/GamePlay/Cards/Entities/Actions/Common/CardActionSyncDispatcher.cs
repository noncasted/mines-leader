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
    /// cards whose sync plays a nested action (MirrorMatch).
    /// </summary>
    public sealed class CardActionSyncDispatcher : ICardActionSyncDispatcher, IScopeSetup
    {
        public CardActionSyncDispatcher(IContainer container)
        {
            _container = container;
        }

        private readonly IContainer _container;
        private readonly Dictionary<Type, ICardActionSync> _byPayload = new();

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            // Resolvers are pulled here rather than injected through the constructor:
            // MirrorMatch's resolver depends back on this dispatcher to play its copied action.
            foreach (var sync in _container.ResolveAll<ICardActionSync>())
                _byPayload.Add(sync.PayloadType, sync);
        }

        public UniTask Dispatch(IReadOnlyLifetime lifetime, ICardActionData data)
        {
            // Every payload of the protocol has a sync (AddAllCardActionSyncs): a missing one is a missing registration.
            if (_byPayload.TryGetValue(data.GetType(), out var sync) == false)
                throw new InvalidOperationException($"No card action sync is registered for {data.GetType().Name}");

            return sync.Sync(lifetime, data);
        }
    }
}
