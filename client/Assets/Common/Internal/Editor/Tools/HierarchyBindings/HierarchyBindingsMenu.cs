using UnityEditor;
using UnityEngine;

namespace Internal
{
    public static class HierarchyBindingsMenu
    {
        private const string HierarchyPath = "GameObject/Create Object Bindings";
        private const string ToolsPath = "Tools/Object Bindings/Create For Selection";

        // Валидатор обязан быть без параметров, иначе Unity не регистрирует пункт целиком.
        // Работаем от Selection, а не от MenuCommand: так пункт вызывается один раз,
        // а не по разу на каждый выделенный объект.
        [MenuItem(HierarchyPath, false, 30)]
        private static void OpenFromHierarchy()
        {
            Open();
        }

        [MenuItem(HierarchyPath, true)]
        private static bool ValidateHierarchy()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem(ToolsPath, false, 30)]
        private static void OpenFromTools()
        {
            Open();
        }

        [MenuItem(ToolsPath, true)]
        private static bool ValidateTools()
        {
            return Selection.activeGameObject != null;
        }

        private static void Open()
        {
            var target = Selection.activeGameObject;

            if (target == null)
            {
                Debug.LogWarning("[HierarchyBindingsGenerator] Select a GameObject first.");
                return;
            }

            HierarchyBindingsWindow.Open(target);
        }
    }
}