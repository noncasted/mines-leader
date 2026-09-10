using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    public class UserTab : ProjectToolsTab
    {
        private const string UserIdKey = "userId";
        private const string EmptyUserId = "<none>";

        private static string EditorUserIdKey => $"userId:{Application.dataPath.GetHashCode()}";

        private Label _userIdValueLabel;
        private Label _userIdEditorValueLabel;

        public override string Title => "User";

        protected override void BuildContent(VisualElement parent)
        {
            var section = BuildSubSection("Local User");

            _userIdEditorValueLabel = BuildUserIdRow(section, $"Editor ({EditorUserIdKey})");
            _userIdValueLabel = BuildUserIdRow(section, $"Shared ({UserIdKey})");

            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("assets-button-row");

            buttonRow.Add(BuildActionButton("Copy", OnCopyUserIdClicked));
            buttonRow.Add(BuildActionButton("Refresh", RefreshUserId));
            buttonRow.Add(BuildActionButton("Clear", OnClearUserIdClicked));

            section.Add(buttonRow);
            parent.Add(section);

            RefreshUserId();
        }

        private static Button BuildActionButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("assets-button");

            return button;
        }

        private static Label BuildUserIdRow(VisualElement parent, string title)
        {
            var label = new Label(title);
            label.AddToClassList("user-id-title");
            parent.Add(label);

            var value = new Label(EmptyUserId) { selection = { isSelectable = true } };
            value.AddToClassList("user-id-value");
            parent.Add(value);

            return value;
        }

        private void RefreshUserId()
        {
            if (_userIdValueLabel == null)
                return;

            _userIdEditorValueLabel.text = ReadUserId(EditorUserIdKey);
            _userIdValueLabel.text = ReadUserId(UserIdKey);
        }

        private void OnCopyUserIdClicked()
        {
            var userId = ReadUserId(EditorUserIdKey);

            if (userId == EmptyUserId)
                userId = ReadUserId(UserIdKey);

            if (userId == EmptyUserId)
            {
                Host.SetStatus("No saved user id", true);
                return;
            }

            EditorGUIUtility.systemCopyBuffer = userId;
            Host.SetStatus("User id copied", false);
        }

        private void OnClearUserIdClicked()
        {
            var confirmed = EditorUtility.DisplayDialog(
                "Clear User Id",
                "Delete the saved local user id? A new user will be created on the next authentication.",
                "Clear",
                "Cancel");

            if (confirmed == false)
                return;

            PlayerPrefs.DeleteKey(EditorUserIdKey);
            PlayerPrefs.DeleteKey(UserIdKey);
            PlayerPrefs.Save();

            RefreshUserId();
            Host.SetStatus("User id cleared", false);
            Debug.Log("[ProjectTools] Saved user id cleared");
        }

        private static string ReadUserId(string key)
        {
            var value = PlayerPrefs.GetString(key, string.Empty);
            return string.IsNullOrEmpty(value) ? EmptyUserId : value;
        }
    }
}