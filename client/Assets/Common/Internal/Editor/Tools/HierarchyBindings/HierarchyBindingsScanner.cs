using System;
using System.Collections.Generic;
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
            var transform = node.Target.transform;

            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index).gameObject;
                var childPath = node.HierarchyPath + "/" + child.name;

                if (TryMakeBindingsField(child, childPath, used, errors, out var boundary))
                {
                    node.Fields.Add(boundary);
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

                var propertyName = HierarchyBindingsNaming.ToIdentifier(child.name);

                if (Validate(propertyName, child.name, childPath, errors) == false)
                    continue;

                propertyName = HierarchyBindingsNaming.MakeUnique(propertyName, used);

                // Имя вложенного класса живёт в том же пространстве имён, что и свойства,
                // поэтому его тоже разводим через общий набор занятых имён.
                var childTypeName = HierarchyBindingsNaming.MakeUnique(propertyName + "Bindings", used);

                var childNode = ScanNode(child, childTypeName, childPath, depth + 1, errors);
                childNode.PropertyName = propertyName;
                childNode.FieldName = HierarchyBindingsNaming.ToFieldName(propertyName);
                node.Children.Add(childNode);
            }
        }

        // Единственная граница: у ребёнка есть свои биндинги, значит за его содержимое отвечает
        // не этот класс. Наружу выдаём ссылку на его компонент.
        private static bool TryMakeBindingsField(
            GameObject child,
            string childPath,
            HashSet<string> used,
            List<string> errors,
            out HierarchyBindingsField field)
        {
            field = null;

            var bindings = FindBindings(child);

            if (bindings == null)
                return false;

            // Имя берём у объекта, а не у типа: снаружи это такой же ребёнок, как остальные,
            // просто за его внутренности отвечает собственный класс биндингов.
            field = MakeField(bindings, child.name, used, errors, childPath);

            if (field != null)
                field.Comment = childPath + " (own bindings)";

            return true;
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