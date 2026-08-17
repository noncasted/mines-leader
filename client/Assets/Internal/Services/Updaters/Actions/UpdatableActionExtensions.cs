using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    public static class UpdatableActionExtensions
    {
        public static UniTask RunFixedAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Func<bool> predicate,
            Action<float> callback)
        {
            var action = new FixedUpdatableActions(lifetime, updater, callback, predicate);
            return action.Process();
        }

        public static UniTask RunUpdateAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Func<bool> predicate,
            Action<float> callback)
        {
            var action = new UpdatableAction(lifetime, updater, callback, predicate);
            return action.Process();
        }

        public static UniTask RunUpdateAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Action<float> callback)
        {
            var action = new UpdatableAction(lifetime, updater, callback, () => true);
            return action.Process();
        }

        public static UniTask RunUpdateAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            float time,
            Action<float> callback)
        {
            var startTime = Time.time;
            var endTime = startTime + time;

            var action = new UpdatableAction(lifetime, updater, callback, () => Time.time < endTime);
            return action.Process();
        }
    }
}