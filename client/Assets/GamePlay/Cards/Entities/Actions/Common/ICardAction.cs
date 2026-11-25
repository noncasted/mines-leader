using System;
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
        Type PayloadType { get; }

        UniTask Sync(IReadOnlyLifetime lifetime, ICardActionData data);
    }

    public readonly struct CardActionResult
    {
        public CardActionResult(bool isSuccess, ICardUsePayload payload)
        {
            IsSuccess = isSuccess;
            Payload = payload;
        }

        public bool IsSuccess { get; }
        public ICardUsePayload Payload { get; }
    }

    public static class CardActionExtensions
    {
        public static ServiceRegistration AddCardActionSyncResolver<TImplementation, TData>(this IBuilder builder)
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

            public Type PayloadType => typeof(TData);

            public UniTask Sync(IReadOnlyLifetime lifetime, ICardActionData data)
            {
                if (data is not TData typedPayload)
                {
                    throw new InvalidCastException(
                        $"Payload type {data.GetType()} does not match expected type {typeof(TData)}.");
                }

                return _implementation.Sync(lifetime, typedPayload);
            }
        }
    }
}