using Flow.Mocks;
using Flow.Startup;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [InitializeOnLoad]
    public static class MockSwitcher
    {
        static MockSwitcher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            // Моки лежат и в сценах, которые грузит обычный старт. Он успевает загрузить меню
            // до EnteredPlayMode, и мок из этой сцены поднял бы второй бутстрап поверх первого.
            if (Object.FindAnyObjectByType<GameStartup>() != null)
                return;

            var mock = Object.FindAnyObjectByType<MockBase>();

            if (mock == null)
                return;

            mock.Process().Forget();
        }
    }
}