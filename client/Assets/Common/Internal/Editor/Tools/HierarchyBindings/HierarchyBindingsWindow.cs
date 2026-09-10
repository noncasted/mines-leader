using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Одно окно и на первую генерацию, и на Regenerate: если биндинги на объекте уже есть,
    // имя и неймспейс подставляются из них и не редактируются, чтобы не плодить второй класс.
    public sealed class HierarchyBindingsWindow : EditorWindow
    {
        private GameObject _target;
        private string _bindingsName;
        private string _namespace;
        private bool _isSceneService;
        private bool _isEntityComponent;
        private bool _isRegenerate;

        public static void Open(GameObject target)
        {
            var window = CreateInstance<HierarchyBindingsWindow>();
            window.titleContent = new GUIContent("Object Bindings");
            window.Setup(target);
            window.minSize = new Vector2(420f, 150f);
            window.ShowUtility();
        }

        private void Setup(GameObject target)
        {
            _target = target;

            var bindings = FindBindings(target);
            var existing = HierarchyBindingsGenerator.ResolveGeneratedType(bindings);

            if (existing != null)
            {
                _bindingsName = existing.Name;
                _namespace = existing.Namespace ?? string.Empty;
                _isSceneService = bindings.IsSceneService;
                _isEntityComponent = bindings.IsEntityComponent;
                _isRegenerate = true;
                return;
            }

            _bindingsName = HierarchyBindingsNaming.ToIdentifier(target.name);
            _namespace = HierarchyBindingsPaths.ResolveDefaultNamespace(target);
        }

        private void OnGUI()
        {
            if (_target == null)
            {
                Close();
                return;
            }

            EditorGUILayout.LabelField("Object", _target.name);

            using (new EditorGUI.DisabledScope(_isRegenerate))
            {
                _bindingsName = EditorGUILayout.TextField("Bindings Name", _bindingsName);
                _namespace = EditorGUILayout.TextField("Namespace", _namespace);
            }

            _isSceneService = EditorGUILayout.Toggle("Is Scene Service", _isSceneService);
            _isEntityComponent = EditorGUILayout.Toggle("Is Entity Component", _isEntityComponent);

            var typeName = HierarchyBindingsGenerator.ToTypeName(_bindingsName);
            EditorGUILayout.LabelField("Class", string.IsNullOrEmpty(typeName) ? "<invalid name>" : typeName);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(typeName)))
            {
                if (GUILayout.Button(_isRegenerate ? "Regenerate" : "Generate"))
                {
                    var target = _target;
                    var name = _bindingsName;
                    var namespaceName = _namespace;
                    var isSceneService = _isSceneService;
                    var isEntityComponent = _isEntityComponent;
                    Close();
                    HierarchyBindingsGenerator.Generate(target, name, namespaceName, isSceneService, isEntityComponent);
                }
            }
        }

        private static ObjectBindings FindBindings(GameObject target)
        {
            return target.GetComponent<ObjectBindings>();
        }
    }
}