using System.Collections.Generic;
using System.IO;
using System.Linq;
using Internal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tools.EditorTools {
    public class ProjectToolsWindow : EditorWindow {
        private static readonly string UssPath = "Assets/Tools/EditorTools/ProjectTools/ProjectToolsWindow.uss";
        private static readonly string[] FavoriteSceneNames = { "Menu", "Game_Field" };

        private OptionsContainer _options;
        private VisualElement _root;
        private Label _statusLabel;

        [MenuItem("Tools/Project Tools %g")]
        public static void ToggleWindow() {
            var existing = Resources.FindObjectsOfTypeAll<ProjectToolsWindow>();

            if (existing.Length > 0) {
                existing[0].Close();
                return;
            }

            var window = GetWindow<ProjectToolsWindow>();
            window.titleContent = new GUIContent("Project Tools");
            window.minSize = new Vector2(420, 500);
        }

        public void CreateGUI() {
            _options = OptionsContainer.Load();
            BuildUI();
        }

        private void BuildUI() {
            _root = new VisualElement();
            _root.AddToClassList("project-tools-root");
            rootVisualElement.Add(_root);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            if (styleSheet != null)
                _root.styleSheets.Add(styleSheet);

            BuildHeader();
            BuildContent();
            BuildFooter();
        }

        private void BuildHeader() {
            var header = new VisualElement();
            header.AddToClassList("header");

            var title = new Label("Project Tools");
            title.AddToClassList("header-title");
            header.Add(title);

            var saveButton = new Button(OnSaveClicked) { text = "Save" };
            saveButton.AddToClassList("save-button");
            header.Add(saveButton);

            _root.Add(header);
        }

        private void BuildContent() {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("content");

            BuildScenesSection(scroll);
            BuildAssetsSection(scroll);
            BuildDebugSection(scroll);
            BuildVersionSection(scroll);
            BuildBackendSection(scroll);
            BuildPlatformSection(scroll);

            _root.Add(scroll);
        }

        private void BuildScenesSection(VisualElement parent) {
            var foldout = new Foldout { text = "Scenes", value = true };
            foldout.AddToClassList("section");

            var scenes = FindAllScenes();
            var favorites = new List<string>();
            var others = new List<string>();

            foreach (var scene in scenes) {
                var sceneName = Path.GetFileNameWithoutExtension(scene);

                if (FavoriteSceneNames.Contains(sceneName))
                    favorites.Add(scene);
                else
                    others.Add(scene);
            }

            favorites = favorites
                .OrderBy(s => System.Array.IndexOf(FavoriteSceneNames, Path.GetFileNameWithoutExtension(s)))
                .ToList();

            if (favorites.Count > 0) {
                var favoritesGrid = BuildSceneGrid(favorites);
                foldout.Add(favoritesGrid);

                var separator = new VisualElement();
                separator.AddToClassList("scene-separator");
                foldout.Add(separator);
            }

            var othersGrid = BuildSceneGrid(others);
            foldout.Add(othersGrid);

            parent.Add(foldout);
        }

        private VisualElement BuildSceneGrid(IEnumerable<string> scenes) {
            var grid = new VisualElement();
            grid.AddToClassList("scene-grid");

            foreach (var scene in scenes) {
                var sceneName = Path.GetFileNameWithoutExtension(scene);
                var button = new Button(() => OpenScene(scene)) { text = sceneName };
                button.AddToClassList("scene-button");

                if (IsActiveScene(scene))
                    button.AddToClassList("scene-button--active");

                grid.Add(button);
            }

            return grid;
        }

        private void BuildAssetsSection(VisualElement parent) {
            var foldout = new Foldout { text = "Assets Options" };
            foldout.AddToClassList("section");

            var toggle = new Toggle("Use Addressables") { value = _options.AssetsOptions.UseAddressables };
            toggle.RegisterValueChangedCallback(e => _options.AssetsOptions.UseAddressables = e.newValue);
            foldout.Add(toggle);

            parent.Add(foldout);
        }

        private void BuildDebugSection(VisualElement parent) {
            var foldout = new Foldout { text = "Debug Options" };
            foldout.AddToClassList("section");

            var gizmos = new Toggle("Enable Gizmos") { value = _options.DebugOptions.EnableGizmos };
            gizmos.RegisterValueChangedCallback(e => _options.DebugOptions.EnableGizmos = e.newValue);
            foldout.Add(gizmos);

            var logs = new Toggle("Enable Logs") { value = _options.DebugOptions.EnableLogs };
            logs.RegisterValueChangedCallback(e => _options.DebugOptions.EnableLogs = e.newValue);
            foldout.Add(logs);

            parent.Add(foldout);
        }

        private void BuildVersionSection(VisualElement parent) {
            var foldout = new Foldout { text = "Version Options" };
            foldout.AddToClassList("section");

            var field = new TextField("Version") { value = _options.VersionOptions.Value };
            field.RegisterValueChangedCallback(e => _options.VersionOptions.Value = e.newValue);
            foldout.Add(field);

            parent.Add(foldout);
        }

        private void BuildBackendSection(VisualElement parent) {
            var foldout = new Foldout { text = "Backend Options" };
            foldout.AddToClassList("section");

            var envField = new EnumField("Environment", _options.BackendOptions.Environment);
            envField.RegisterValueChangedCallback(e => _options.BackendOptions.Environment = (BackendEnvironment)e.newValue);
            foldout.Add(envField);

            var prodUrl = new TextField("Production URL") { value = _options.BackendOptions.ProductionApiUrl };
            prodUrl.RegisterValueChangedCallback(e => _options.BackendOptions.ProductionApiUrl = e.newValue);
            foldout.Add(prodUrl);

            var localUrl = new TextField("Local URL") { value = _options.BackendOptions.LocalApiUrl };
            localUrl.RegisterValueChangedCallback(e => _options.BackendOptions.LocalApiUrl = e.newValue);
            foldout.Add(localUrl);

            parent.Add(foldout);
        }

        private void BuildPlatformSection(VisualElement parent) {
            var foldout = new Foldout { text = "Platform Options" };
            foldout.AddToClassList("section");

            var platformField = new EnumField("Platform Type", _options.PlatformOptions.PlatformType);
            platformField.RegisterValueChangedCallback(e => _options.PlatformOptions.PlatformType = (PlatformType)e.newValue);
            foldout.Add(platformField);

            parent.Add(foldout);
        }

        private void BuildFooter() {
            var footer = new VisualElement();
            footer.AddToClassList("footer");

            _statusLabel = new Label("Ready");
            _statusLabel.AddToClassList("status-label");
            footer.Add(_statusLabel);

            _root.Add(footer);
        }

        private void OnSaveClicked() {
            _options.Save();
            UpdateStatus("Settings saved", false);
            Debug.Log("[ProjectTools] Settings saved");
        }

        private void UpdateStatus(string message, bool isError) {
            if (_statusLabel == null)
                return;

            _statusLabel.text = message;
            _statusLabel.style.color = isError
                ? new Color(1f, 0.3f, 0.3f)
                : new Color(0.7f, 0.7f, 0.7f);
        }

        private static List<string> FindAllScenes() {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] {
                "Assets/Common", "Assets/GamePlay", "Assets/Global",
                "Assets/Internal", "Assets/Loop", "Assets/Menu",
                "Assets/Meta", "Assets/Startup", "Assets/Tools"
            });

            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(Path.GetFileNameWithoutExtension)
                .ToList();
        }

        private static bool IsActiveScene(string scenePath) {
            var active = EditorSceneManager.GetActiveScene();
            return active.path == scenePath;
        }

        private static void OpenScene(string scenePath) {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(scenePath);
        }
    }
}
