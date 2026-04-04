using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    public class OptionsEditorWindow : EditorWindow
    {
        private readonly string _uXmlPath = "Assets/Internal/Editor/UI/OptionsEditorWindow.uxml";
        private readonly string _ussPath = "Assets/Internal/Editor/UI/OptionsEditorWindow.uss";
        private readonly string _settingsPath = "Assets/Internal/Options/InternalSettings.asset";

        private SerializedObject _serializedSettings;
        private OptionsContainer _optionsContainer;
        private Label _statusLabel;
        private VisualElement _root;

        [MenuItem("Tools/Internal/Options Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<OptionsEditorWindow>();
            window.titleContent = new GUIContent("Options Editor");
            window.minSize = new Vector2(400, 500);
        }

        public void CreateGUI()
        {
            LoadOrCreateSettings();
            SetupUI();
        }

        private void LoadOrCreateSettings()
        {
            _optionsContainer = AssetDatabase.LoadAssetAtPath<OptionsContainer>(_settingsPath);

            if (_optionsContainer == null)
            {
                _optionsContainer = CreateInstance<OptionsContainer>();
                AssetDatabase.CreateAsset(_optionsContainer, _settingsPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Internal] [Options] Created new OptionsContainer at " + _settingsPath);
            }

            _serializedSettings = new SerializedObject(_optionsContainer);
        }

        private void SetupUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(_uXmlPath);

            if (visualTree == null)
            {
                Debug.LogError("[Internal] [Options] Failed to load UXML at " + _uXmlPath);
                UpdateStatus("Error: UXML not found", true);
                return;
            }

            _root = visualTree.CloneTree();
            rootVisualElement.Add(_root);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(_ussPath);

            if (styleSheet != null)
            {
                _root.styleSheets.Add(styleSheet);
            }

            SetupDataBinding();
            SetupEventHandlers();
        }

        private void SetupDataBinding()
        {
            if (_serializedSettings == null)
            {
                Debug.LogError("[Internal] [Options] SerializedObject is null, cannot bind data");
                UpdateStatus("Error: Settings not loaded", true);
                return;
            }

            _root.Bind(_serializedSettings);
            _statusLabel = _root.Q<Label>("status-label");
            UpdateStatus("Ready", false);
        }

        private void SetupEventHandlers()
        {
            var saveButton = _root.Q<Button>("save-button");

            if (saveButton != null)
            {
                saveButton.clicked += OnSaveClicked;
            }

            _root.TrackSerializedObjectValue(_serializedSettings, OnSettingsChanged);
        }

        private void OnSaveClicked()
        {
            if (_serializedSettings == null || _optionsContainer == null)
            {
                UpdateStatus("Error: Settings not loaded", true);
                return;
            }

            _serializedSettings.ApplyModifiedProperties();
            EditorUtility.SetDirty(_optionsContainer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UpdateStatus("Settings saved successfully", false);
            Debug.Log("[Internal] [Options] Settings saved to " + _settingsPath);
        }

        private void OnSettingsChanged(SerializedObject obj)
        {
            UpdateStatus("Modified (unsaved)", false);
        }

        private void UpdateStatus(string message, bool isError)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message;
                _statusLabel.style.color = isError
                    ? new Color(1f, 0.3f, 0.3f)
                    : new Color(0.7f, 0.7f, 0.7f);
            }
        }

        private void OnDestroy()
        {
            if (_serializedSettings != null)
            {
                _serializedSettings.Dispose();
            }
        }
    }
}