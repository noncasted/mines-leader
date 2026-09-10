using System.Text;

namespace Internal
{
    // Имена групп и свойств во всех каталогах строятся одинаково: только буквы и цифры,
    // с префиксом-заглушкой, если идентификатор начинается с цифры.
    public static class CatalogNaming
    {
        public static string ToIdentifier(string name, string digitPrefix)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var identifier = Sanitize(name);

            if (identifier.Length == 0)
                return string.Empty;

            if (char.IsDigit(identifier[0]))
                return digitPrefix + identifier;

            return identifier;
        }

        public static string ToGroupName(string name)
        {
            return ToIdentifier(name, "G");
        }

        public static string Sanitize(string name)
        {
            var builder = new StringBuilder(name.Length);

            foreach (var character in name)
            {
                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
            }

            return builder.ToString();
        }
    }
}