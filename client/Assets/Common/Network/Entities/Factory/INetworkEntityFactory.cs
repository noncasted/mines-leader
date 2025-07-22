using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using Shared;

namespace Common.Network
{
    public interface INetworkEntityFactory
    {
        INetworkUser LocalUser { get; }
        INetworkEntityIds Ids { get; }

        void ListenRemote<T>(
            IReadOnlyLifetime lifetime,
            Func<IReadOnlyLifetime, RemoteEntityData, UniTask<INetworkEntity>> listener);

        UniTask Send(IReadOnlyLifetime lifetime, INetworkEntity entity, IEntityPayload payload);

        UniTask CreateRemote(IReadOnlyLifetime lifetime, RemoteEntityData data);
    }

    public static class NetworkEntityFactoryExtensions
    {
        public static UniTask Send<T>(
            this INetworkEntityFactory factory,
            IReadOnlyLifetime lifetime,
            INetworkEntity entity)
            where T : IEntityPayload, new()
        {
            return factory.Send(lifetime, entity, new T());
        }

        public static IEntityBuilder AddLocalEntity(this IEntityBuilder builder, INetworkEntityFactory factory)
        {
            builder.Register<NetworkEntity>()
                .WithParameter(factory.LocalUser)
                .WithParameter(factory.Ids.GetEntityId())
                .As<INetworkEntity>();

            return builder;
        }

        public static IEntityBuilder AddRemoteEntity(this IEntityBuilder builder, RemoteEntityData data)
        {
            builder.Register<NetworkEntity>()
                .WithParameter(data.Owner)
                .WithParameter(data.Id)
                .As<INetworkEntity>();

            return builder;
        }

        public static IEntityScopeResult FillProperties(this IEntityScopeResult result, RemoteEntityData data)
        {
            var properties = result.Get<INetworkEntity>().Properties;

            if (properties.Count != data.RawProperties.Count)
                throw new InvalidOperationException(
                    $"Properties count mismatch local: {properties.Count} != remote: {data.RawProperties.Count}");

            foreach (var rawProperty in data.RawProperties)
                properties[rawProperty.PropertyId].Update(rawProperty.Value);

            return result;
        }
    }

    public class RemoteEntityData
    {
        public RemoteEntityData(
            INetworkUser owner,
            int id,
            IReadOnlyList<SharedSessionObject.PropertyUpdate> rawProperties,
            byte[] payload)
        {
            Owner = owner;
            Id = id;
            RawProperties = rawProperties;
            Payload = payload;
        }

        public INetworkUser Owner { get; }
        public int Id { get; }
        public IReadOnlyList<SharedSessionObject.PropertyUpdate> RawProperties { get; }
        public byte[] Payload { get; }

        public T ReadPayload<T>() where T : IEntityPayload, new()
        {
            return (T)MemoryPackSerializer.Deserialize<IEntityPayload>(Payload);
        }
    }
}