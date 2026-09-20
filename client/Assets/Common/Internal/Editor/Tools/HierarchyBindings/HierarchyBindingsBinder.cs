using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Ссылки заполняются по тому же дереву, из которого сгенерирован код, поэтому имена полей
    // совпадают по построению и искать ничего не нужно.
    internal static class HierarchyBindingsBinder
    {
        public static bool Bind(GameObject root, Type type, HierarchyBindingsRequest request, List<string> errors)
        {
            var component = ResolveComponent(root, type, errors);

            if (component == null)
                return false;

            var node = HierarchyBindingsScanner.Scan(root, request.TypeName, errors);

            if (node == null)
                return false;

            var serializedObject = new SerializedObject(component);

            if (Fill(serializedObject, null, node, errors) == false)
                return false;

            var hash = serializedObject.FindProperty("_structureHash");

            if (hash == null)
            {
                errors.Add("Generated field '_structureHash' is missing. The code is not compiled yet.");
                return false;
            }

            hash.stringValue = HierarchyBindingsStructure.ComputeHash(node);

            // Галочки держим на компоненте синхронно с тем, что реально сгенерировано.
            SetFlag(serializedObject, "_isSceneService", request.IsSceneService, errors);
            SetFlag(serializedObject, "_isEntityComponent", request.IsEntityComponent, errors);

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            return true;
        }

        // На первой генерации пользовательский класс ещё не наследует биндинги, поэтому компонент
        // добавляется отдельно. После того как наследование дописали, лишний компонент снимаем сами:
        // иначе на объекте окажется два носителя биндингов.
        private static Component ResolveComponent(GameObject root, Type type, List<string> errors)
        {
            Component derived = null;
            Component exact = null;

            foreach (var candidate in root.GetComponents<Component>())
            {
                if (candidate == null || candidate is IObjectBindings == false)
                    continue;

                if (candidate.GetType() == type)
                {
                    exact = candidate;
                    continue;
                }

                if (type.IsInstanceOfType(candidate) == false)
                {
                    errors.Add($"'{root.name}' already carries other bindings: {candidate.GetType().Name}.");
                    return null;
                }

                derived = candidate;
            }

            if (derived == null)
                return exact != null ? exact : root.AddComponent(type);

            if (exact != null)
            {
                UnityEngine.Object.DestroyImmediate(exact, true);

                Debug.Log(
                    $"[HierarchyBindingsGenerator] Removed standalone {type.Name} from '{root.name}': {derived.GetType().Name} inherits it.");
            }

            return derived;
        }

        private static void SetFlag(
            SerializedObject serializedObject,
            string fieldName,
            bool value,
            List<string> errors)
        {
            var property = serializedObject.FindProperty(fieldName);

            if (property == null)
            {
                errors.Add($"Generated field '{fieldName}' is missing.");
                return;
            }

            property.boolValue = value;
        }

        private static bool Fill(
            SerializedObject serializedObject,
            SerializedProperty parent,
            HierarchyBindingsNode node,
            List<string> errors)
        {
            foreach (var field in node.Fields)
            {
                var property = Find(serializedObject, parent, field.FieldName);

                if (property == null)
                {
                    errors.Add($"Generated field '{field.FieldName}' is missing. The code is not compiled yet.");
                    return false;
                }

                if (field.IsArray == false)
                {
                    property.objectReferenceValue = field.Target;
                    continue;
                }

                property.arraySize = field.Targets.Count;

                for (var index = 0; index < field.Targets.Count; index++)
                    property.GetArrayElementAtIndex(index).objectReferenceValue = field.Targets[index];
            }

            foreach (var child in node.Children)
            {
                var property = Find(serializedObject, parent, child.FieldName);

                if (property == null)
                {
                    errors.Add($"Generated field '{child.FieldName}' is missing. The code is not compiled yet.");
                    return false;
                }

                if (child.IsArray == false)
                {
                    if (Fill(serializedObject, property, child, errors) == false)
                        return false;

                    continue;
                }

                // Класс у элементов ряда общий, а объекты разные: размер массива задаём один раз
                // и заполняем каждый элемент по его собственному узлу.
                property.arraySize = child.Elements.Count;

                for (var index = 0; index < child.Elements.Count; index++)
                {
                    var element = property.GetArrayElementAtIndex(index);

                    if (Fill(serializedObject, element, child.Elements[index], errors) == false)
                        return false;
                }
            }

            return true;
        }

        private static SerializedProperty Find(
            SerializedObject serializedObject,
            SerializedProperty parent,
            string fieldName)
        {
            return parent == null
                ? serializedObject.FindProperty(fieldName)
                : parent.FindPropertyRelative(fieldName);
        }
    }
}