using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Services
{
    public interface ISnapshotHandler<T> where T : IMoveSnapshotRecord
    {
        UniTask Handle(T record);
    }

    public static class SnapshotHandlerExtensions
    {
        public static void AddSnapshotHandler<THandler, TRecord>(this IScopeBuilder builder)
            where THandler : ISnapshotHandler<TRecord>
            where TRecord : IMoveSnapshotRecord
        {
            builder.Register<THandler>()
                   .As<ISnapshotHandler<TRecord>>()
                   .WithParameter(builder.Lifetime);

            builder.Register<Resolver<TRecord>>()
                   .AsSelfResolvable();
        }

        public class Resolver<T> where T : IMoveSnapshotRecord
        {
            private static readonly ILog _log = Logs.Of<Resolver<T>>();

            public Resolver(ISnapshotReceiver receiver, ISnapshotHandler<T> handler)
            {
                _handler = handler;
                receiver.Add(typeof(T), Handle);
            }

            private readonly ISnapshotHandler<T> _handler;
            private readonly string _name;

            private UniTask Handle(IMoveSnapshotRecord moveRecord)
            {
                if (moveRecord is not T record)
                {
                    throw new ArgumentException(
                        $"[Snapshot] Expected record of type {typeof(T).Name}, but got {moveRecord.GetType().Name}.",
                        nameof(moveRecord));
                }

                _log.Debug("Handling record of type {Type}.", typeof(T).Name);

                return _handler.Handle(record);
            }
        }
    }
}