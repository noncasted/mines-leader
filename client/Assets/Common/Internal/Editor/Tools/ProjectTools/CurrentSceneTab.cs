using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Internal
{
    public class CurrentSceneTab : ProjectToolsTab
    {
        private Label _summaryLabel;

        public override string Title => "Current Scene";

        public override void OnEnable()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        public override void OnDisable()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        protected override void BuildContent(VisualElement parent)
        {
            var section = BuildSubSection("Object Bindings");

            _summaryLabel = new Label();
            section.Add(_summaryLabel);

            var button = new Button(OnRegenerateClicked) { text = "Regenerate All Bindings" };
            button.AddToClassList("assets-run-all-button");
            section.Add(button);

            parent.Add(section);
            Refresh();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_summaryLabel == null)
                return;

            var scene = SceneManager.GetActiveScene();
            _summaryLabel.text = $"Scene '{scene.name}': {HierarchyBindingsBatch.Count(scene)} bindings";
        }

        // Перегенерация перезапускает компиляцию, так что несохранённые правки сцены надо
        // закоммитить заранее: связывание после перезагрузки домена работает с тем, что на диске.
        private void OnRegenerateClicked()
        {
            var scene = SceneManager.GetActiveScene();

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                Host.SetStatus("Regeneration cancelled: the scene has unsaved changes", true);
                return;
            }

            var message = HierarchyBindingsBatch.RegenerateScene(scene);
            Host.SetStatus(message, message.StartsWith("Regenerating") == false);
            Refresh();
        }
    }
}
