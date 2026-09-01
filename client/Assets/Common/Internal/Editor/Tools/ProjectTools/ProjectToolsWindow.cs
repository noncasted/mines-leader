using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    public interface IProjectToolsHost
    {
        OptionsContainer Options { get; }

        void SetStatus(string message, bool isError);
    }
    
    public class ProjectToolsWindow : EditorWindow, IProjectToolsHost
    {
        private const string UssPath = "Assets/Common/Internal/Editor/Tools/ProjectTools/ProjectToolsWindow.uss";

        private readonly List<ProjectToolsTab> _tabs = new()
        {
            new ScenesTab(),
            new OptionsTab(),
            new AssetsTab(),
            new UserTab()
        };

        private readonly List<VisualElement> _tabContents = new();
        private readonly List<Button> _tabButtons = new();

        private OptionsContainer _options;
        private VisualElement _root;
        private Label _statusLabel;

        public OptionsContainer Options => _options;

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
            _options = LoadOptions();
            BuildUI();
        }

        public void SetStatus(string message, bool isError)
        {
            if (_statusLabel == null)
                return;

            _statusLabel.text = message;

            _statusLabel.style.color = isError
                ? new Color(1f, 0.3f, 0.3f)
                : new Color(0.7f, 0.7f, 0.7f);
        }

        private void OnEnable()
        {
            foreach (var tab in _tabs)
                tab.OnEnable();
        }

        private void OnDisable()
        {
            foreach (var tab in _tabs)
                tab.OnDisable();
        }

        private void BuildUI()
        {
            _tabButtons.Clear();
            _tabContents.Clear();

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

            SelectTab(0);
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

            for (int i = 0; i < _tabs.Count; i++)
            {
                int index = i;
                var button = new Button(() => SelectTab(index)) { text = _tabs[i].Title };
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

            foreach (var tab in _tabs)
            {
                var content = tab.Build(this);
                scroll.Add(content);
                _tabContents.Add(content);
            }

            _root.Add(scroll);
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

        private void SelectTab(int index)
        {
            for (int i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].EnableInClassList("tab-button--active", i == index);

            for (int i = 0; i < _tabContents.Count; i++)
                _tabContents[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnSaveClicked()
        {
            if (_options == null)
            {
                SetStatus("Options asset is missing", true);
                return;
            }

            EditorUtility.SetDirty(_options);
            AssetDatabase.SaveAssets();
            SetStatus("Settings saved", false);
            Debug.Log("[ProjectTools] Settings saved");
        }

        private OptionsContainer LoadOptions()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(OptionsContainer)}");

            if (guids.Length == 0)
            {
                Debug.LogError("[ProjectTools] OptionsContainer asset is missing");
                return null;
            }

            if (guids.Length > 1)
                Debug.LogWarning($"[ProjectTools] Found {guids.Length} OptionsContainer assets, using the first one");

            return AssetDatabase.LoadAssetAtPath<OptionsContainer>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
