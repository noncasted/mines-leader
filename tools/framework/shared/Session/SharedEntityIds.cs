using System;

namespace Shared
{
    // Entity ids: every user of a session creates its entities in its own range, so ids never collide without a
    // round trip to the backend. Id = userIndex * Stride + counter, counter 1..Stride-1. User indices start from 1
    // and grow with every new seat of the session (a reconnect keeps the index of its seat); range 0 (the session itself) is
    // reserved for ids issued by the backend (CreateBackend). The backend refuses a CreateRequest with an id out of the range of its
    // creator, and a session refuses users above MaxUserIndex. Service ids are negative (backend Service.GetId).
    public static class SharedEntityIds
    {
        public const int Stride = 1 << 20;

        // The largest index whose range still fits a positive int: 2047 * 2^20 + (2^20 - 1) = int.MaxValue.
        public const int MaxUserIndex = int.MaxValue / Stride;

        public static int Create(int userIndex, int counter)
        {
            if (userIndex < 1 || userIndex > MaxUserIndex)
                throw new ArgumentOutOfRangeException(nameof(userIndex), userIndex, $"User index is out of 1..{MaxUserIndex}");

            if (counter < 1 || counter >= Stride)
                throw new ArgumentOutOfRangeException(nameof(counter), counter, $"Entity counter of user {userIndex} is out of 1..{Stride - 1}");

            return userIndex * Stride + counter;
        }

        // An id issued by the backend (range 0): an entity the backend builds itself (EntityBuilder), also one owned
        // by a user - its client receives it as any remote entity, not as one of its own to restore.
        public static int CreateBackend(int counter)
        {
            if (counter < 1 || counter >= Stride)
                throw new ArgumentOutOfRangeException(nameof(counter), counter, $"Backend entity counter is out of 1..{Stride - 1}");

            return counter;
        }

        public static bool IsBackendIssued(int entityId)
        {
            return entityId > 0 && entityId < Stride;
        }

        // The user whose range holds the id: its creator. Negative for a service id.
        public static int GetUserIndex(int entityId)
        {
            return entityId / Stride;
        }

        public static bool IsInRangeOf(int entityId, int userIndex)
        {
            return entityId > 0 && entityId % Stride != 0 && GetUserIndex(entityId) == userIndex;
        }
    }
}
