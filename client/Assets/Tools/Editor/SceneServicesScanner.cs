using System.Collections.Generic;
using Internal;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tools
{
    public static class SceneServicesScanner
    {
        [MenuItem("Assets/Scan services %e", priority = -1000)]
        private static void ScanServices()
        {
            var targets = new List<ISceneReloadListener>();
            var scenes = GetScenes();

            foreach (var scene in scenes)
            {
                var rootObjects = scene.GetRootGameObjects();

                foreach (var rootObject in rootObjects)
                {
                    var components = rootObject.GetComponentsInChildren<ISceneReloadListener>();
                    targets.AddRange(components);
                }
            }

            foreach (var target in targets)
            {
                var hasChanged = target.OnReload();

                if (hasChanged == true)
                    EditorUtility.SetDirty(target as MonoBehaviour);
            }

            return;

            IReadOnlyList<Scene> GetScenes()
            {
                var foundScenes = new List<Scene>();

                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);

                    if (scene.isLoaded)
                        foundScenes.Add(scene);
                }

                return foundScenes;
            }
        }
    }
}