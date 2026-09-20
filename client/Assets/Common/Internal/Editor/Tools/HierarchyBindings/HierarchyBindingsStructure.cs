using System.Security.Cryptography;
using System.Text;

namespace Internal
{
    // Слепок структуры пишется в сгенерированный компонент, чтобы потом можно было отличить
    // «иерархия та же» от «иерархию поменяли, а Regenerate не нажали».
    internal static class HierarchyBindingsStructure
    {
        public static string ComputeHash(HierarchyBindingsNode root)
        {
            var builder = new StringBuilder();
            Append(builder, root);

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            return System.BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void Append(StringBuilder builder, HierarchyBindingsNode node)
        {
            builder.Append(node.HierarchyPath).Append('|').Append(node.TypeName).Append(';');

            foreach (var field in node.Fields)
            {
                builder.Append(field.PropertyName).Append(':').Append(field.DeclaredTypeName).Append(';');

                // У массива в слепок идут все элементы: иначе переименование или перестановка
                // внутри ряда осталась бы незамеченной.
                foreach (var target in field.Targets)
                    builder.Append(target == null ? "null" : target.name).Append(',');
            }

            foreach (var child in node.Children)
            {
                builder.Append(child.PropertyName).Append(':').Append(child.TypeName);

                if (child.IsArray == false)
                {
                    builder.Append(';');
                    Append(builder, child);
                    continue;
                }

                builder.Append('[').Append(child.Elements.Count).Append("];");

                foreach (var element in child.Elements)
                    Append(builder, element);
            }
        }
    }
}