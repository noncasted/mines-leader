namespace Common.Reactive
{
    public static class LifetimedValueExtensions
    {
        extension<T>(ILifetimedValue<T> property)
        {
            public void View(IReadOnlyLifetime lifetime, Action listener)
            {
                property.Advise(lifetime, (_, _) => listener.Invoke());
                listener.Invoke();
            }

            public void View(IReadOnlyLifetime lifetime, Action<T> listener)
            {
                property.Advise(lifetime, (_, value) => listener.Invoke(value));
                listener.Invoke(property.Value);
            }
        }

        extension<T>(ILifetimedValue<T> property) where T : class
        {
            public void ViewNotNull(
                IReadOnlyLifetime lifetime,
                Action<T> listener)
            {
                property.Advise(lifetime, (_, value) => {
                    if (value != null)
                        listener.Invoke(value);
                });

                if (property.Value != null)
                    listener.Invoke(property.Value);
            }

            public void ViewNotNull(
                IReadOnlyLifetime lifetime,
                Action<IReadOnlyLifetime, T> listener)
            {
                property.Advise(lifetime, (valueLifetime, value) => {
                    if (value != null)
                        listener.Invoke(valueLifetime.Intersect(lifetime), value);
                });

                if (property.Value != null)
                    listener.Invoke(property.ValueLifetime.Intersect(lifetime), property.Value);
            }
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

        extension(ILifetimedValue<bool> property)
        {
            public void AdviseTrue(
                IReadOnlyLifetime lifetime,
                Action listener)
            {
                property.Advise(lifetime, (_, value) => OnChange(value));

                void OnChange(bool value)
                {
                    if (value == false)
                        return;

                    listener.Invoke();
                }
            }

            public Task WaitFalse(IReadOnlyLifetime lifetime)
            {
                if (property.Value == false)
                    return Task.CompletedTask;

                var completion = new TaskCompletionSource();
                lifetime.Listen(() => completion.TrySetCanceled());

                property.Advise(lifetime, (_, value) => OnChange(value));

                return completion.Task;

                void OnChange(bool value)
                {
                    if (value == false)
                    {
                        completion.TrySetResult();
                        return;
                    }

                    throw new Exception();
                }
            }

            public Task WaitTrue(IReadOnlyLifetime lifetime)
            {
                if (property.Value == true)
                    return Task.CompletedTask;

                var completion = new TaskCompletionSource();
                lifetime.Listen(() => completion.TrySetCanceled());

                property.Advise(lifetime, (_, value) => OnChange(value));

                return completion.Task;

                void OnChange(bool value)
                {
                    if (value == true)
                    {
                        completion.TrySetResult();
                        return;
                    }

                    throw new Exception();
                }
            }
        }
    }
}