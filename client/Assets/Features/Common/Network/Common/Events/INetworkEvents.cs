using System;
using System.Collections.Generic;
using Internal;

namespace Common.Network.Common
{
    public interface INetworkEvents
    {
        IReadOnlyDictionary<Type, object> Entries { get; }

        void AddSource(Type type, object source, Action<IEventPayload> invoke);
        void Invoke(byte[] rawPayload);
        void Send(IEventPayload rawPayload);
    }

    public static class NetworkEventsExtensions
    {
        public static IViewableDelegate<T> GetEvent<T>(this INetworkEvents events) where T : IEventPayload
        {
            var type = typeof(T);

            if (events.Entries.ContainsKey(type) == false)
            {
                var source = new ViewableDelegate<T>();
                
                events.AddSource(type, source, payload =>
                {
                    if (payload is not T castedPayload)
                        throw new InvalidCastException();
                    
                    source.Invoke(castedPayload);
                });
            }

            return events.Entries[type] as ViewableDelegate<T>;
        }
    }
}