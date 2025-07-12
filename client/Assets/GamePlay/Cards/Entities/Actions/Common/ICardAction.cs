using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public interface ICardAction
    {
        UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime);
    }

    public interface ICardActionSync<T> where T : ICardActionData
    {
        UniTask Sync(IReadOnlyLifetime lifetime, T payload);
    }

    public interface ICardActionSync
    {
        UniTask Sync(IReadOnlyLifetime lifetime, ICardActionData data);
    }

    public class CardActionResult
    {
        public bool IsSuccess { get; set; }
        public ICardUsePayload Payload { get; set; }
    }

    public static class CardActionExtensions
    {
        public static IRegistration AddCardActionSyncResolver<TImplementation, TData>(this IEntityBuilder builder)
            where TImplementation : ICardActionSync<TData>
            where TData : ICardActionData
        {
            builder.Register<Resolver<TImplementation, TData>>()
                .As<ICardActionSync>()
                .AsSelfResolvable();

            return builder.Register<TImplementation>()
                .As<ICardActionSync<TData>>();
        }

        public class Resolver<TImplementation, TData> : ICardActionSync
            where TImplementation : ICardActionSync<TData>
            where TData : ICardActionData
        {
            public Resolver(TImplementation implementation)
            {
                _implementation = implementation;
            }

            private readonly TImplementation _implementation;

            public UniTask Sync(IReadOnlyLifetime lifetime, ICardActionData data)
            {
                if (data is not TData typedPayload)
                {
                    throw new System.InvalidCastException(
                        $"Payload type {data.GetType()} does not match expected type {typeof(TData)}."
                    );
                }

                return _implementation.Sync(lifetime, typedPayload);
            }
        }
    }
}