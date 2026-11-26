namespace Framework.Tests.Fixtures;

// A list filled by handlers on other threads under its own lock: a test reads it under the same lock.
public static class LockedListExtensions
{
    public static int LockedCount<T>(this List<T> list)
    {
        lock (list)
            return list.Count;
    }
}
