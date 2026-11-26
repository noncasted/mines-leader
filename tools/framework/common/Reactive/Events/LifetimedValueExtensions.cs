using System;

namespace Internal
{
    // The synchronous views of a lifetimed value. The waits are platform extensions: UniTask on the client
    // (ReactiveUniTaskExtensions of the core), Task on the backend (ReactiveTaskExtensions of Common).
    public static class LifetimedValueExtensions
    {
        public static void View<T>(this ILifetimedValue<T> property, IReadOnlyLifetime lifetime, Action listener)
        {
            property.Advise(lifetime, (_, _) => listener.Invoke());
            listener.Invoke();
        }

        public static void View<T>(this ILifetimedValue<T> property, IReadOnlyLifetime lifetime, Action<T> listener)
        {
            property.Advise(lifetime, (_, value) => listener.Invoke(value));
            listener.Invoke(property.Value);
        }

        public static void ViewNotNull<T>(
            this ILifetimedValue<T> property,
            IReadOnlyLifetime lifetime,
            Action<T> listener) where T : class
        {
            property.Advise(lifetime, (_, value) => {
                if (value != null)
                    listener.Invoke(value);
            });

            if (property.Value != null)
                listener.Invoke(property.Value);
        }

        public static void ViewNotNull<T>(
            this ILifetimedValue<T> property,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime, T> listener) where T : class
        {
            property.Advise(lifetime, (valueLifetime, value) => {
                if (value != null)
                    listener.Invoke(valueLifetime.Intersect(lifetime), value);
            });

            if (property.Value != null)
                listener.Invoke(property.ValueLifetime.Intersect(lifetime), property.Value);
        }

        public static void View<T>(
            this ILifetimedValue<T> property,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime, T> listener)
        {
            // The value lifetime given to the listener is bounded by the subscriber lifetime too: whatever the
            // listener subscribes in it ends with the subscription, not only with the next value.
            property.Advise(lifetime, (valueLifetime, value) => listener.Invoke(valueLifetime.Intersect(lifetime), value));
            listener.Invoke(property.ValueLifetime.Intersect(lifetime), property.Value);
        }

        public static void AdviseTrue(
            this ILifetimedValue<bool> property,
            IReadOnlyLifetime lifetime,
            Action listener)
        {
            property.Advise(lifetime, (_, value) => OnChange(value));

            void OnChange(bool value)
            {
                if (value == false)
                    return;

                listener?.Invoke();
            }
        }

        public static void AdviseFalse(
            this ILifetimedValue<bool> property,
            IReadOnlyLifetime lifetime,
            Action listener)
        {
            property.Advise(lifetime, (_, value) => OnChange(value));

            void OnChange(bool value)
            {
                if (value == true)
                    return;

                listener?.Invoke();
            }
        }

        public static void AdviseTrue(
            this ILifetimedValue<bool> property,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime> listener)
        {
            property.Advise(lifetime, OnChange);

            void OnChange(IReadOnlyLifetime valueLifetime, bool value)
            {
                if (value == false)
                    return;

                listener?.Invoke(valueLifetime.Intersect(lifetime));
            }
        }

        public static void ViewTrue(
            this ILifetimedValue<bool> property,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime> listener)
        {
            ILifetime trueLifetime = null;
            property.View(lifetime, (_, value) => OnChange(value));

            return;

            void OnChange(bool value)
            {
                if (value == false)
                {
                    trueLifetime?.Terminate();
                    return;
                }

                // Notify renews a true value: the lifetime given for the previous one ends first.
                trueLifetime?.Terminate();
                trueLifetime = lifetime.Child();
                listener?.Invoke(trueLifetime);
            }
        }
    }
}
