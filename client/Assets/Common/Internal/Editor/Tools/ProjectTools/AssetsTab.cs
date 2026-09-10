using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    public class AssetsTab : ProjectToolsTab
    {
        private ProgressBar _progressBar;

        public override string Title => "Assets";

        protected override void BuildContent(VisualElement parent)
        {
            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("assets-button-row");

            buttonRow.Add(BuildActionButton("Generate Prefabs", OnGeneratePrefabsClicked));
            buttonRow.Add(BuildActionButton("Generate Scenes", OnGenerateScenesClicked));
            buttonRow.Add(BuildActionButton("Generate Sprites", OnGenerateSpritesClicked));
            buttonRow.Add(BuildActionButton("Export Card Icons", OnExportCardIconsClicked));

            parent.Add(buttonRow);

            _progressBar = new ProgressBar { lowValue = 0, highValue = 100, value = 0 };
            _progressBar.AddToClassList("assets-progress");
            parent.Add(_progressBar);

            var runAllButton = new Button(OnRunAllClicked) { text = "Run All" };
            runAllButton.AddToClassList("assets-run-all-button");
            parent.Add(runAllButton);
        }

        private static Button BuildActionButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("assets-button");

            return button;
        }

        private void OnGeneratePrefabsClicked() => Run("Generating prefabs...", "Prefabs generated",
            "Prefab generation failed", PrefabCatalogGenerator.Generate);

        private void OnGenerateScenesClicked() => Run("Generating scenes...", "Scenes generated",
            "Scene generation failed", SceneGenerator.Generate);

        private void OnGenerateSpritesClicked() => Run("Generating sprites...", "Sprites generated",
            "Sprite generation failed", SpriteGenerator.Generate);

        private void OnExportCardIconsClicked() => Run("Exporting card icons...", "Card icons exported",
            "Card icons export failed", CardIconsExporter.Export);

        private void OnRunAllClicked()
        {
            Run("Running all generators...", "All generators completed", "Run All failed", () => {
                _progressBar.value = 0;

                _progressBar.value = 25;
                PrefabCatalogGenerator.Generate();

                _progressBar.value = 50;
                SceneGenerator.Generate();

                _progressBar.value = 75;
                SpriteGenerator.Generate();

                _progressBar.value = 100;
            });
        }

        private void Run(string startedMessage, string finishedMessage, string failedMessage, Action action)
        {
            try
            {
                Host.SetStatus(startedMessage, false);
                action();
                Host.SetStatus(finishedMessage, false);
            }
            catch (Exception ex)
            {
                Host.SetStatus(failedMessage, true);
                Debug.LogError($"[ProjectTools] {failedMessage}: {ex}");
            }
        }
    }
}