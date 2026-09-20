using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // Имена свойств берутся из имён объектов в иерархии и типов компонентов, поэтому их надо
    // приводить к идентификатору C#. Отдельно проверяем совпадение с членами MonoBehaviour:
    // сгенерированный класс сам наследует MonoBehaviour, а пользовательский класс наследует его,
    // так что совпадение имени даёт CS0108 в чужом файле и ищется потом неприятно.
    [NoAutoStaticsCleanup]
    public static class HierarchyBindingsNaming
    {
        private const string DigitPrefix = "N";

        private static HashSet<string> _reservedMembers;

        public static string ToIdentifier(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var builder = new StringBuilder(raw.Length);
            var startOfWord = true;

            foreach (var character in raw)
            {
                if (char.IsLetterOrDigit(character) == false)
                {
                    startOfWord = true;
                    continue;
                }

                // Первую букву слова поднимаем, остальные оставляем как есть: "Text (TMP)" даёт
                // "TextTMP", а не "TextTmp".
                builder.Append(startOfWord ? char.ToUpperInvariant(character) : character);
                startOfWord = false;
            }

            if (builder.Length == 0)
                return string.Empty;

            if (char.IsDigit(builder[0]))
                builder.Insert(0, DigitPrefix);

            return builder.ToString();
        }

        // Аббревиатуры в начале опускаем целиком: UIElementPointerHandler даёт
        // _uiElementPointerHandler, а не _uIElementPointerHandler.
        public static string ToFieldName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return string.Empty;

            var run = 0;

            while (run < propertyName.Length && char.IsUpper(propertyName[run]))
                run++;

            // Последняя заглавная из серии начинает следующее слово, если за ней идёт строчная.
            if (run > 1 && run < propertyName.Length)
                run--;

            if (run == 0)
                run = 1;

            var builder = new StringBuilder(propertyName.Length + 1);
            builder.Append('_');

            for (var index = 0; index < propertyName.Length; index++)
                builder.Append(index < run ? char.ToLowerInvariant(propertyName[index]) : propertyName[index]);

            return builder.ToString();
        }

        // Дубликаты имён в одном классе разводим суффиксом. Порядок обхода детерминирован,
        // поэтому суффикс стабилен между прогонами, пока иерархия не менялась.
        public static string MakeUnique(string name, HashSet<string> used)
        {
            if (used.Add(name))
                return name;

            for (var index = 2; index < 1000; index++)
            {
                var candidate = name + index;

                if (used.Add(candidate))
                    return candidate;
            }

            return name;
        }

        // Unity плодит братьев суффиксом-номером: "Entry", "Entry_1", "Entry (2)". Для группы
        // нужно общее имя, поэтому номер вместе с разделителем и скобками отрезаем. Имя без номера
        // остаётся как есть, так что первый брат и остальные дают одну базу.
        public static string StripIndexSuffix(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            var end = raw.Length;

            while (end > 0 && (raw[end - 1] == ')' || char.IsWhiteSpace(raw[end - 1])))
                end--;

            var digitsEnd = end;

            while (end > 0 && char.IsDigit(raw[end - 1]))
                end--;

            // Цифр не было — это не номер, а обычное имя.
            if (end == digitsEnd)
                return raw;

            while (end > 0 && (raw[end - 1] == '(' || raw[end - 1] == '_' || raw[end - 1] == '-' ||
                               char.IsWhiteSpace(raw[end - 1])))
                end--;

            // Имя целиком было номером — отрезать нечего.
            return end == 0 ? raw : raw.Substring(0, end);
        }

        // Массив называется во множественном числе, иначе Entries и Entry в одном классе не различить.
        public static string ToPlural(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var last = char.ToLowerInvariant(name[name.Length - 1]);
            var previous = name.Length > 1 ? char.ToLowerInvariant(name[name.Length - 2]) : '\0';

            if (last == 'y' && IsVowel(previous) == false)
                return name.Substring(0, name.Length - 1) + "ies";

            if (last == 's' || last == 'x' || last == 'z' ||
                (previous == 'c' && last == 'h') || (previous == 's' && last == 'h'))
                return name + "es";

            return name + "s";
        }

        private static bool IsVowel(char character)
        {
            return character == 'a' || character == 'e' || character == 'i' ||
                   character == 'o' || character == 'u';
        }

        public static bool IsReservedMember(string name)
        {
            _reservedMembers ??= CollectReservedMembers();
            return _reservedMembers.Contains(name);
        }

        private static HashSet<string> CollectReservedMembers()
        {
            var reserved = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(IObjectBindings.StructureHash)
            };

            const BindingFlags flags = BindingFlags.Public |
                                       BindingFlags.NonPublic |
                                       BindingFlags.Instance |
                                       BindingFlags.Static |
                                       BindingFlags.FlattenHierarchy;

            foreach (var member in typeof(MonoBehaviour).GetMembers(flags))
                reserved.Add(member.Name);

            return reserved;
        }
    }
}