namespace Shared
{
    // Code of a lobby room: 6 characters, A-Z without I and O plus digits 2-9 (no look-alike characters).
    public static class LobbyCode
    {
        public const int Length = 6;
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        // What the user typed -> the form the backend compares: no spaces around, upper case.
        public static string Normalize(string code)
        {
            return code == null ? string.Empty : code.Trim().ToUpperInvariant();
        }

        // Expects a normalized code.
        public static bool IsValid(string code)
        {
            if (code == null || code.Length != Length)
                return false;

            foreach (var symbol in code)
            {
                if (Alphabet.IndexOf(symbol) < 0)
                    return false;
            }

            return true;
        }
    }
}
