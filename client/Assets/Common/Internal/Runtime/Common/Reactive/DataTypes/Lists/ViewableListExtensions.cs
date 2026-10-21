using System;

namespace Internal
{
    public static class ViewableListExtensions
    {
        public static void View<T>(this IViewableList<T> list, IReadOnlyLifetime lifetime, Action<T> listener)
        {
            list.Advise(lifetime, (_, value) => listener.Invoke(value));

            foreach (var entry in list)
                listener.Invoke(entry);
        }

        public static void View<T>(
            this IViewableList<T> list,
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