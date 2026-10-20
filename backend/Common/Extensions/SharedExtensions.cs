namespace Common.Extensions;

public static class SharedExtensions
{
    public static T ThrowIfNull<T>(this T? obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        return obj;
    }
}