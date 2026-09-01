using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Internal
{
    public class ScenesTab : ProjectToolsTab
    {
        private static readonly string[] FavoriteSceneNames = { "Startup", "Menu", "Game_Field" };

        private readonly Dictionary<string, Button> _sceneButtons = new();

        public override string Title => "Scenes";

        public override void OnEnable()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        public override void OnDisable()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
        }

        protected override void BuildContent(VisualElement parent)
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
                        .OrderBy(s => Array.IndexOf(FavoriteSceneNames, Path.GetFileNameWithoutExtension(s)))
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

        private void OnSceneOpened(Scene scene, OpenSceneMode mode) => RefreshSceneButtons();

        private void OnSceneClosed(Scene scene) => RefreshSceneButtons();

        private VisualElement BuildSceneGrid(IEnumerable<string> scenes)
        {
            var grid = new VisualElement();
            grid.AddToClassList("scene-grid");

            foreach (var scene in scenes)
            {
                var sceneName = Path.GetFileNameWithoutExtension(scene);
                var scenePath = scene;
                var button = new Button { text = sceneName };
                button.AddToClassList("scene-button");
                button.tooltip = "Click to open, Ctrl+Click to open additively";

                button.RegisterCallback<ClickEvent>(evt => {
                    if (evt.ctrlKey || evt.actionKey)
                        OpenSceneAdditive(scenePath);
                    else
                        OpenScene(scenePath);

                    RefreshSceneButtons();
                });

                _sceneButtons[scenePath] = button;
                grid.Add(button);
            }

            RefreshSceneButtons();

            return grid;
        }

        private void RefreshSceneButtons()
        {
            foreach (var pair in _sceneButtons)
            {
                if (pair.Value == null)
                    continue;

                pair.Value.EnableInClassList("scene-button--active", IsActiveScene(pair.Key));

                pair.Value.EnableInClassList("scene-button--loaded",
                    IsLoadedScene(pair.Key) && !IsActiveScene(pair.Key));
            }
        }

        private static List<string> FindAllScenes()
        {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });

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

        private static bool IsLoadedScene(string scenePath)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).path == scenePath)
                    return true;
            }

            return false;
        }

        private static void OpenScene(string scenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(scenePath);
        }

        private static void OpenSceneAdditive(string scenePath)
        {
            if (IsLoadedScene(scenePath))
            {
                var scene = SceneManager.GetSceneByPath(scenePath);

                if (scene == EditorSceneManager.GetActiveScene() && SceneManager.sceneCount <= 1)
                    return;

                if (EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { scene }))
                    EditorSceneManager.CloseScene(scene, true);

                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }
    }
}