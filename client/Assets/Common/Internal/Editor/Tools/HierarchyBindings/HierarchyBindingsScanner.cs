using System;
using System.Collections.Generic;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // Полное зеркало иерархии: генератор обходит объект сверху донизу, включая содержимое
    // вложенных префабов. Границ всего две — объект со своими биндингами (за его внутренности
    // отвечает собственный класс, и ссылка на него обнуляет глубину сериализации, которой у Unity
    // всего семь уровней) и HierarchyBindingsIgnoreChildren, которым обрубают ненужные ветки.
    [NoAutoStaticsCleanup]
    internal static class HierarchyBindingsScanner
    {
        // Unity обрывает сериализацию вложенных не-Object типов глубже семи уровней, поэтому
        // дальше по дереву спускаться бессмысленно: поля просто не сохранятся.
        public const int MaxNestingDepth = 7;

        // Минимальный размер группы. Пара одинаковых братьев чаще всего осмысленно называется
        // по отдельности (Left/Right), а вот ряд из трёх и длиннее — это уже список.
        public const int MinGroupSize = 3;

        private static readonly HashSet<Type> SkippedComponents = new()
        {
            typeof(CanvasRenderer),
            typeof(HierarchyBindingsIgnoreChildren)
        };

        public static HierarchyBindingsNode Scan(GameObject root, string rootTypeName, List<string> errors)
        {
            if (root == null)
            {
                errors.Add("Object is null.");
                return null;
            }

            var node = ScanNode(root, rootTypeName, root.name, 0, errors);
            return errors.Count == 0 ? node : null;
        }

        private static HierarchyBindingsNode ScanNode(
            GameObject target,
            string typeName,
            string hierarchyPath,
            int depth,
            List<string> errors)
        {
            var node = new HierarchyBindingsNode
            {
                Target = target,
                TypeName = typeName,
                HierarchyPath = hierarchyPath
            };

            node.IgnoreChildren = target.GetComponent<HierarchyBindingsIgnoreChildren>() != null;

            var used = new HashSet<string>(StringComparer.Ordinal);
            CollectGameObject(node, used);
            CollectComponents(node, used, depth, errors);

            if (node.IgnoreChildren == false)
                CollectChildren(node, used, depth, errors);

            return node;
        }

        // Сам объект нужен всем: у вложенных классов другого пути к нему нет, а у корня
        // свойство просто дублирует MonoBehaviour.gameObject ради единообразия обращения.
        private static void CollectGameObject(HierarchyBindingsNode node, HashSet<string> used)
        {
            var propertyName = HierarchyBindingsNaming.MakeUnique("GameObject", used);

            node.Fields.Add(new HierarchyBindingsField
            {
                Target = node.Target,
                PropertyName = propertyName,
                FieldName = HierarchyBindingsNaming.ToFieldName(propertyName),
                CodeTypeName = "global::UnityEngine.GameObject"
            });
        }

        private static void CollectComponents(
            HierarchyBindingsNode node,
            HashSet<string> used,
            int depth,
            List<string> errors)
        {
            foreach (var component in node.Target.GetComponents<Component>())
            {
                if (component == null)
                {
                    errors.Add($"'{node.HierarchyPath}' has a missing script. Fix the object before generating.");
                    continue;
                }

                var type = component.GetType();

                if (SkippedComponents.Contains(type))
                    continue;

                // Владелец биндингов — это сам генерируемый компонент (или пользовательский класс,
                // который его наследует). Ссылка на самого себя в зеркале не нужна.
                if (depth == 0 && component is IObjectBindings)
                    continue;

                var field = MakeField(component, type.Name, used, errors, node.HierarchyPath);

                if (field != null)
                    node.Fields.Add(field);
            }
        }

        private static void CollectChildren(
            HierarchyBindingsNode node,
            HashSet<string> used,
            int depth,
            List<string> errors)
        {
            var scans = ScanChildren(node, depth, errors);

            for (var index = 0; index < scans.Count;)
            {
                var length = GroupLength(scans, index);

                if (length >= MinGroupSize && TryAddGroup(node, scans, index, length, used, errors))
                {
                    index += length;
                    continue;
                }

                AddSingle(node, scans[index], used, errors);
                index++;
            }
        }

        // Имена на уровне родителя раздаём только после того, как известно, что во что схлопнется:
        // иначе занятые имена достались бы братьям, которых в классе уже не будет.
        private static List<ChildScan> ScanChildren(HierarchyBindingsNode node, int depth, List<string> errors)
        {
            var transform = node.Target.transform;
            var scans = new List<ChildScan>(transform.childCount);

            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index).gameObject;
                var childPath = node.HierarchyPath + "/" + child.name;
                var scan = new ChildScan
                {
                    Child = child,
                    ChildPath = childPath,
                    BaseName = HierarchyBindingsNaming.StripIndexSuffix(child.name)
                };

                var bindings = FindBindings(child);

                if (bindings != null)
                {
                    scan.Boundary = bindings;
                    scan.Signature = "boundary:" + ToCodeTypeName(bindings.GetType());
                    scans.Add(scan);
                    continue;
                }

                if (depth + 1 > MaxNestingDepth)
                {
                    errors.Add(
                            $"'{childPath}' is deeper than {MaxNestingDepth} levels. Unity stops serializing there — " +
                            "give an intermediate object its own bindings to break the chain."
                        );
                    continue;
                }

                scan.Node = ScanNode(child, null, childPath, depth + 1, errors);
                scan.Signature = ComputeSignature(scan.Node);
                scans.Add(scan);
            }

            return scans;
        }

        // Схлопываем только подряд идущих братьев с общим именем и совпадающим содержимым: массив
        // должен читаться так же, как ряд в иерархии, а класс — один на всех.
        private static int GroupLength(List<ChildScan> scans, int start)
        {
            var first = scans[start];

            if (string.IsNullOrEmpty(HierarchyBindingsNaming.ToIdentifier(first.BaseName)))
                return 1;

            var length = 1;

            while (start + length < scans.Count)
            {
                var next = scans[start + length];

                if (string.Equals(next.BaseName, first.BaseName, StringComparison.Ordinal) == false)
                    break;

                if (string.Equals(next.Signature, first.Signature, StringComparison.Ordinal) == false)
                    break;

                length++;
            }

            return length;
        }

        private static bool TryAddGroup(
            HierarchyBindingsNode node,
            List<ChildScan> scans,
            int start,
            int length,
            HashSet<string> used,
            List<string> errors)
        {
            var first = scans[start];
            var baseName = HierarchyBindingsNaming.ToIdentifier(first.BaseName);

            if (Validate(baseName, first.BaseName, first.ChildPath, errors) == false)
                return false;

            var propertyName = HierarchyBindingsNaming.MakeUnique(
                HierarchyBindingsNaming.ToPlural(baseName), used);
            var fieldName = HierarchyBindingsNaming.ToFieldName(propertyName);
            var comment = $"{first.ChildPath} .. {scans[start + length - 1].Child.name} ({length} items)";

            if (first.Boundary != null)
            {
                var field = new HierarchyBindingsField
                {
                    Target = first.Boundary,
                    PropertyName = propertyName,
                    FieldName = fieldName,
                    CodeTypeName = ToCodeTypeName(first.Boundary.GetType()),
                    Comment = comment + " (own bindings)"
                };

                for (var index = start; index < start + length; index++)
                    field.Targets.Add(scans[index].Boundary);

                node.Fields.Add(field);
                return true;
            }

            var template = first.Node;
            template.TypeName = HierarchyBindingsNaming.MakeUnique(baseName + "Bindings", used);
            template.PropertyName = propertyName;
            template.FieldName = fieldName;

            for (var index = start; index < start + length; index++)
                template.Elements.Add(scans[index].Node);

            node.Children.Add(template);
            return true;
        }

        private static void AddSingle(
            HierarchyBindingsNode node,
            ChildScan scan,
            HashSet<string> used,
            List<string> errors)
        {
            if (scan.Boundary != null)
            {
                // Имя берём у объекта, а не у типа: снаружи это такой же ребёнок, как остальные,
                // просто за его внутренности отвечает собственный класс биндингов.
                var field = MakeField(scan.Boundary, scan.Child.name, used, errors, scan.ChildPath);

                if (field == null)
                    return;

                field.Comment = scan.ChildPath + " (own bindings)";
                node.Fields.Add(field);
                return;
            }

            var propertyName = HierarchyBindingsNaming.ToIdentifier(scan.Child.name);

            if (Validate(propertyName, scan.Child.name, scan.ChildPath, errors) == false)
                return;

            propertyName = HierarchyBindingsNaming.MakeUnique(propertyName, used);

            // Имя вложенного класса живёт в том же пространстве имён, что и свойства,
            // поэтому его тоже разводим через общий набор занятых имён.
            scan.Node.TypeName = HierarchyBindingsNaming.MakeUnique(propertyName + "Bindings", used);
            scan.Node.PropertyName = propertyName;
            scan.Node.FieldName = HierarchyBindingsNaming.ToFieldName(propertyName);
            node.Children.Add(scan.Node);
        }

        // Содержимое узла без его собственного имени: по нему решается, обслуживает ли один класс
        // всех братьев сразу.
        private static string ComputeSignature(HierarchyBindingsNode node)
        {
            var builder = new StringBuilder();
            AppendSignature(builder, node);
            return builder.ToString();
        }

        private static void AppendSignature(StringBuilder builder, HierarchyBindingsNode node)
        {
            foreach (var field in node.Fields)
                builder.Append(field.PropertyName).Append(':').Append(field.DeclaredTypeName).Append(';');

            foreach (var child in node.Children)
            {
                builder.Append(child.PropertyName).Append(child.IsArray ? "[" + child.Elements.Count + "]" : "");
                builder.Append('{');
                AppendSignature(builder, child);
                builder.Append('}');
            }
        }

        private sealed class ChildScan
        {
            public GameObject Child;
            public string ChildPath;
            public string BaseName;
            public string Signature;

            // У ребёнка свои биндинги: внутрь не идём, наружу выдаём ссылку на его компонент.
            public Component Boundary;
            public HierarchyBindingsNode Node;
        }

        private static Component FindBindings(GameObject target)
        {
            foreach (var component in target.GetComponents<Component>())
            {
                if (component is IObjectBindings)
                    return component;
            }

            return null;
        }

        private static HierarchyBindingsField MakeField(
            Component component,
            string rawName,
            HashSet<string> used,
            List<string> errors,
            string hierarchyPath)
        {
            var propertyName = HierarchyBindingsNaming.ToIdentifier(rawName);

            if (Validate(propertyName, rawName, hierarchyPath, errors) == false)
                return null;

            propertyName = HierarchyBindingsNaming.MakeUnique(propertyName, used);

            return new HierarchyBindingsField
            {
                Target = component,
                PropertyName = propertyName,
                FieldName = HierarchyBindingsNaming.ToFieldName(propertyName),
                CodeTypeName = ToCodeTypeName(component.GetType())
            };
        }

        private static bool Validate(string propertyName, string rawName, string hierarchyPath, List<string> errors)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                errors.Add($"'{hierarchyPath}': name '{rawName}' is not a C# identifier.");
                return false;
            }

            if (HierarchyBindingsNaming.IsReservedMember(propertyName))
            {
                errors.Add(
                    $"'{hierarchyPath}': '{propertyName}' collides with a MonoBehaviour member. Rename the object.");
                return false;
            }

            return true;
        }

        public static string ToCodeTypeName(Type type)
        {
            var fullName = type.FullName ?? type.Name;
            return "global::" + fullName.Replace('+', '.');
        }
    }
}