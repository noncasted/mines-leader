namespace Common.Reactive
{
    public static class ViewableListExtensions
    {
        extension<T>(IViewableList<T> list)
        {
            public void View(IReadOnlyLifetime lifetime, Action<T> listener)
            {
                list.Advise(lifetime, (_, value) => listener.Invoke(value));

                foreach (var entry in list)
                    listener.Invoke(entry);
            }

            public void View(
                IReadOnlyLifetime lifetime,
                Action<IReadOnlyLifetime, T> listener)
            {
                // The entry lifetime given to the listener is bounded by the subscriber lifetime too.
                list.Advise(lifetime, (entryLifetime, value) => listener.Invoke(entryLifetime.Intersect(lifetime), value));

                foreach (var entry in list)
                    listener.Invoke(list.GetLifetime(entry).Intersect(lifetime), entry);
            }
        }
    }
}