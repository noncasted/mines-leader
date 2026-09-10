using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // Общая обвязка инспекторов каталогов: сбор импортеров выделения, блок в шапке
    // и поля, умеющие показывать смешанное значение при мультивыделении.
    public static class CatalogInspectorGUI
    {
        private const float LabelWidth = 50f;

        public static bool TryCollectImporters(
            Editor editor,
            Func<AssetImporter, bool> isCatalogTarget,
            out List<AssetImporter> importers)
        {
            importers = null;

            if (editor == null || editor.targets == null || editor.targets.Length == 0)
                return false;

            var collected = new List<AssetImporter>(editor.targets.Length);

            foreach (var target in editor.targets)
            {
                if (TryGetImporter(target, out var importer) == false)
                    return false;

                if (isCatalogTarget(importer) == false)
                    return false;

                collected.Add(importer);
            }

            importers = collected;
            return true;
        }

        public static Section BeginSection()
        {
            return new Section();
        }

        // Возвращает true, если пользователь изменил значение; next тогда содержит новое.
        public static bool TryDraw<T>(bool mixed, Func<T> draw, out T next)
        {
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            next = draw();
            var changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            return changed;
        }

        public static bool TryDrawToggle(string label, bool value, bool mixed, out bool next)
        {
            return TryDraw(mixed, () => EditorGUILayout.ToggleLeft(label, value, EditorStyles.boldLabel), out next);
        }

        public static bool TryDrawPopup(
            string label,
            IReadOnlyList<string> options,
            int index,
            bool mixed,
            out int next)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);

            var labels = new string[options.Count];

            for (var i = 0; i < options.Count; i++)
                labels[i] = options[i];

            var changed = TryDraw(mixed, () => EditorGUILayout.Popup(Mathf.Max(index, 0), labels), out next);

            EditorGUILayout.EndHorizontal();
            return changed && next >= 0 && next < options.Count;
        }

        private static bool TryGetImporter(Object target, out AssetImporter importer)
        {
            importer = null;

            // PrefabImporter is internal in this Unity version; AssetImporter covers it.
            if (target is AssetImporter assetImporter)
            {
                importer = assetImporter;
                return true;
            }

            if (target is not GameObject gameObject)
                return false;

            if (EditorUtility.IsPersistent(gameObject) == false)
                return false;

            if (PrefabUtility.IsPartOfPrefabAsset(gameObject) == false)
                return false;

            if (gameObject.transform.parent != null)
                return false;

            var path = AssetDatabase.GetAssetPath(gameObject);

            if (string.IsNullOrEmpty(path))
                return false;

            importer = AssetImporter.GetAtPath(path);
            return importer != null;
        }

        // Шапка инспектора рисуется с выключенным GUI, а поля каталога должны быть кликабельны.
        public sealed class Section : IDisposable
        {
            private readonly bool _wasEnabled;
            private readonly float _previousLabelWidth;

            public Section()
            {
                _wasEnabled = GUI.enabled;
                _previousLabelWidth = EditorGUIUtility.labelWidth;

                GUI.enabled = true;
                EditorGUIUtility.labelWidth = LabelWidth;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            }

            public void Dispose()
            {
                EditorGUILayout.EndVertical();
                EditorGUIUtility.labelWidth = _previousLabelWidth;
                GUI.enabled = _wasEnabled;
            }
        }
    }
}