using System.Collections.Generic;
using System.IO;
using System.Linq;
using Internal;
using Tools.SceneBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tools
{
    public class ProjectToolsWindow : EditorWindow
    {
        private static readonly string UssPath = "Assets/Tools/Editor/ProjectTools/ProjectToolsWindow.uss";
        private static readonly string[] FavoriteSceneNames = { "Menu", "Game_Field" };

        private OptionsContainer _options;
        private VisualElement _root;
        private Label _statusLabel;
        private ProgressBar _progressBar;

        private readonly List<VisualElement> _tabContents = new();
        private readonly List<Button> _tabButtons = new();

        [MenuItem("Tools/Project Tools %g")]
        public static void ToggleWindow()
        {
            var existing = Resources.FindObjectsOfTypeAll<ProjectToolsWindow>();

            foreach (var window in existing)
            {
                try
                {
                    if (window != null)
                    {
                        window.Close();
                        return;
                    }
                }
                catch
                {
                    // Zombie window, skip and create new
                }
            }

            var newWindow = GetWindow<ProjectToolsWindow>();
            newWindow.titleContent = new GUIContent("Project Tools");
            newWindow.minSize = new Vector2(420, 500);
        }

        public void CreateGUI()
        {
            _options = OptionsContainer.Load();
            BuildUI();
        }

        private void BuildUI()
        {
            _root = new VisualElement();
            _root.AddToClassList("project-tools-root");
            rootVisualElement.Add(_root);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            if (styleSheet != null)
                _root.styleSheets.Add(styleSheet);

            BuildHeader();
            BuildTabBar();
            BuildTabContents();
            BuildFooter();
        }

        private void BuildHeader()
        {
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

        private void BuildTabBar()
        {
            var tabBar = new VisualElement();
            tabBar.AddToClassList("tab-bar");

            var tabNames = new[] { "Scenes", "Options", "Assets" };

            for (int i = 0; i < tabNames.Length; i++)
            {
                int index = i;
                var button = new Button(() => SelectTab(index)) { text = tabNames[i] };
                button.AddToClassList("tab-button");
                tabBar.Add(button);
                _tabButtons.Add(button);
            }

            _root.Add(tabBar);
        }

        private void BuildTabContents()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("tab-content-wrapper");

            var scenesTab = new VisualElement();
            scenesTab.AddToClassList("tab-content");
            BuildScenesSection(scenesTab);
            scroll.Add(scenesTab);
            _tabContents.Add(scenesTab);

            var optionsTab = new VisualElement();
            optionsTab.AddToClassList("tab-content");
            BuildOptionsSection(optionsTab);
            scroll.Add(optionsTab);
            _tabContents.Add(optionsTab);

            var assetsTab = new VisualElement();
            assetsTab.AddToClassList("tab-content");
            BuildAssetsSection(assetsTab);
            scroll.Add(assetsTab);
            _tabContents.Add(assetsTab);

            _root.Add(scroll);
            SelectTab(0);
        }

        private void SelectTab(int index)
        {
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                if (i == index)
                    _tabButtons[i].AddToClassList("tab-button--active");
                else
                    _tabButtons[i].RemoveFromClassList("tab-button--active");
            }

            for (int i = 0; i < _tabContents.Count; i++)
            {
                _tabContents[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void BuildScenesSection(VisualElement parent)
        {
            var scenes = FindAllScenes();
            var favorites = new List<string>();
            var others = new List<string>();

            foreach (var scene in scenes)
            {
                var sceneName = Path.GetFileNameWithoutExtension(scene);

                if (FavoriteSceneNames.Contains(sceneName))
                    favorites.Add(scene);
                else
                    others.Add(scene);
            }

            favorites = favorites
                        .OrderBy(s => System.Array.IndexOf(FavoriteSceneNames, Path.GetFileNameWithoutExtension(s)))
                        .ToList();

            if (favorites.Count > 0)
            {
                var favoritesGrid = BuildSceneGrid(favorites);
                parent.Add(favoritesGrid);

                var separator = new VisualElement();
                separator.AddToClassList("scene-separator");
                parent.Add(separator);
            }

            var othersGrid = BuildSceneGrid(others);
            parent.Add(othersGrid);
        }

        private VisualElement BuildSceneGrid(IEnumerable<string> scenes)
        {
            var grid = new VisualElement();
            grid.AddToClassList("scene-grid");

            foreach (var scene in scenes)
            {
                var sceneName = Path.GetFileNameWithoutExtension(scene);
                var button = new Button(() => OpenScene(scene)) { text = sceneName };
                button.AddToClassList("scene-button");

                if (IsActiveScene(scene))
                    button.AddToClassList("scene-button--active");

                grid.Add(button);
            }

            return grid;
        }

        private void BuildOptionsSection(VisualElement parent)
        {
            var assetsSection = BuildSubSection("Assets Options");
            var toggle = new Toggle("Use Addressables") { value = _options.AssetsOptions.UseAddressables };
            toggle.RegisterValueChangedCallback(e => _options.AssetsOptions.UseAddressables = e.newValue);
            assetsSection.Add(toggle);
            parent.Add(assetsSection);

            var debugSection = BuildSubSection("Debug Options");
            var gizmos = new Toggle("Enable Gizmos") { value = _options.DebugOptions.EnableGizmos };
            gizmos.RegisterValueChangedCallback(e => _options.DebugOptions.EnableGizmos = e.newValue);
            debugSection.Add(gizmos);

            var logs = new Toggle("Enable Logs") { value = _options.DebugOptions.EnableLogs };
            logs.RegisterValueChangedCallback(e => _options.DebugOptions.EnableLogs = e.newValue);
            debugSection.Add(logs);
            parent.Add(debugSection);

            var versionSection = BuildSubSection("Version Options");
            var field = new TextField("Version") { value = _options.VersionOptions.Value };
            field.RegisterValueChangedCallback(e => _options.VersionOptions.Value = e.newValue);
            versionSection.Add(field);
            parent.Add(versionSection);

            var backendSection = BuildSubSection("Backend Options");
            var envField = new EnumField("Environment", _options.BackendOptions.Environment);

            envField.RegisterValueChangedCallback(e =>
                _options.BackendOptions.Environment = (BackendEnvironment)e.newValue);
            backendSection.Add(envField);

            var prodUrl = new TextField("Production URL") { value = _options.BackendOptions.ProductionApiUrl };
            prodUrl.RegisterValueChangedCallback(e => _options.BackendOptions.ProductionApiUrl = e.newValue);
            backendSection.Add(prodUrl);

            var localUrl = new TextField("Local URL") { value = _options.BackendOptions.LocalApiUrl };
            localUrl.RegisterValueChangedCallback(e => _options.BackendOptions.LocalApiUrl = e.newValue);
            backendSection.Add(localUrl);
            parent.Add(backendSection);

            var platformSection = BuildSubSection("Platform Options");
            var platformField = new EnumField("Platform Type", _options.PlatformOptions.PlatformType);

            platformField.RegisterValueChangedCallback(e =>
                _options.PlatformOptions.PlatformType = (PlatformType)e.newValue);
            platformSection.Add(platformField);
            parent.Add(platformSection);
        }

        private static VisualElement BuildSubSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            var label = new Label(title);
            label.AddToClassList("section-title");
            section.Add(label);

            return section;
        }

        private void BuildAssetsSection(VisualElement parent)
        {
            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("assets-button-row");

            var generatePrefabsButton = new Button(OnGeneratePrefabsClicked) { text = "Generate Prefabs" };
            generatePrefabsButton.AddToClassList("assets-button");
            buttonRow.Add(generatePrefabsButton);

            var generateScenesButton = new Button(OnGenerateScenesClicked) { text = "Generate Scenes" };
            generateScenesButton.AddToClassList("assets-button");
            buttonRow.Add(generateScenesButton);
            var exportIconsButton = new Button(OnExportCardIconsClicked) { text = "Export Card Icons" };
            exportIconsButton.AddToClassList("assets-button");
            buttonRow.Add(exportIconsButton);

            parent.Add(buttonRow);

            _progressBar = new ProgressBar { lowValue = 0, highValue = 100, value = 0 };
            _progressBar.AddToClassList("assets-progress");
            parent.Add(_progressBar);

            var runAllButton = new Button(OnRunAllClicked) { text = "Run All" };
            runAllButton.AddToClassList("assets-run-all-button");
            parent.Add(runAllButton);
        }

        private void OnGeneratePrefabsClicked()
        {
            try
            {
                UpdateStatus("Generating prefabs...", false);
                PrefabGenerator.Generate();
                UpdateStatus("Prefabs generated", false);
            }
            catch (System.Exception ex)
            {
                UpdateStatus("Prefab generation failed", true);
                Debug.LogError($"[ProjectTools] Prefab generation failed: {ex}");
            }
        }

        private void OnGenerateScenesClicked()
        {
            try
            {
                UpdateStatus("Generating scenes...", false);
                SceneGenerator.Generate();
                UpdateStatus("Scenes generated", false);
            }
            catch (System.Exception ex)
            {
                UpdateStatus("Scene generation failed", true);
                Debug.LogError($"[ProjectTools] Scene generation failed: {ex}");
            }
        }

        private void OnExportCardIconsClicked()
        {
            try
            {
                UpdateStatus("Exporting card icons...", false);
                CardIconsExporter.Export();
                UpdateStatus("Card icons exported", false);
            }
            catch (System.Exception ex)
            {
                UpdateStatus("Card icons export failed", true);
                Debug.LogError($"[ProjectTools] Card icons export failed: {ex}");
            }
        }

        private void OnRunAllClicked()
        {
            try
            {
                UpdateStatus("Running all generators...", false);
                _progressBar.value = 0;

                _progressBar.value = 25;
                PrefabGenerator.Generate();

                _progressBar.value = 75;
                SceneGenerator.Generate();

                _progressBar.value = 100;
                UpdateStatus("All generators completed", false);
            }
            catch (System.Exception ex)
            {
                UpdateStatus("Run All failed", true);
                Debug.LogError($"[ProjectTools] Run All failed: {ex}");
            }
        }

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("footer");

            _statusLabel = new Label("Ready");
            _statusLabel.AddToClassList("status-label");
            footer.Add(_statusLabel);

            _root.Add(footer);
        }

        private void OnSaveClicked()
        {
            _options.Save();
            UpdateStatus("Settings saved", false);
            Debug.Log("[ProjectTools] Settings saved");
        }

        private void UpdateStatus(string message, bool isError)
        {
            if (_statusLabel == null)
                return;

            _statusLabel.text = message;

            _statusLabel.style.color = isError
                ? new Color(1f, 0.3f, 0.3f)
                : new Color(0.7f, 0.7f, 0.7f);
        }

        private static List<string> FindAllScenes()
        {
            var guids = AssetDatabase.FindAssets("t:Scene", new[]
            {
                "Assets/Common", "Assets/GamePlay", "Assets/Global",
                "Assets/Internal", "Assets/Loop", "Assets/Menu",
                "Assets/Meta", "Assets/Startup", "Assets/Tools"
            });

            return guids
                   .Select(AssetDatabase.GUIDToAssetPath)
                   .OrderBy(Path.GetFileNameWithoutExtension)
                   .ToList();
        }

        private static bool IsActiveScene(string scenePath)
        {
            var active = EditorSceneManager.GetActiveScene();
            return active.path == scenePath;
        }

        private static void OpenScene(string scenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(scenePath);
        }
    }
}