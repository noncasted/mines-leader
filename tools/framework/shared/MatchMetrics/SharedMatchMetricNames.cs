namespace Shared
{
    // Names, tags and texts of fields are the only text a stream carries, so no free text gets through them: names and keys
    // [a-z0-9._], values and texts [a-z0-9._-], of a limited length. The one rule of the client and the server.
    public static class SharedMatchMetricNames
    {
        public static bool IsName(string text)
        {
            return IsValid(text, SharedMatchMetricsLimits.MaxNameLength, false);
        }

        public static bool IsKey(string text)
        {
            return IsValid(text, SharedMatchMetricsLimits.MaxTagLength, false);
        }

        public static bool IsValue(string text)
        {
            return IsValid(text, SharedMatchMetricsLimits.MaxTagLength, true);
        }

        private static bool IsValid(string text, int maxLength, bool allowDash)
        {
            if (string.IsNullOrEmpty(text) || text.Length > maxLength)
                return false;

            for (var i = 0; i < text.Length; i++)
            {
                var symbol = text[i];

                var isValid = symbol >= 'a' && symbol <= 'z'
                              || symbol >= '0' && symbol <= '9'
                              || symbol == '.'
                              || symbol == '_'
                              || symbol == '-' && allowDash;

                if (isValid == false)
                    return false;
            }

            return true;
        }
    }
}
