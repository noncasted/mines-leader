using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    // Между записью кода и заполнением ссылок происходит перезагрузка домена, поэтому объект
    // приходится описывать строками и находить заново. Префаб при этом открываем содержимым:
    // так правка уезжает в сам ассет, а не в оверрайд открытой сцены.
    internal sealed class HierarchyBindingsTarget : IDisposable
    {
        public GameObject Root { get; private set; }

        private GameObject _loadedContents;
        private string _assetPath;
        private Scene _scene;

        public static bool TryDescribe(
            GameObject target,
            out string assetPath,
            out string objectPath,
            out bool isPrefabAsset,
            out string error)
        {
            assetPath = string.Empty;
            objectPath = string.Empty;
            isPrefabAsset = false;
            error = string.Empty;

            var stage = PrefabStageUtility.GetPrefabStage(target);

            if (stage != null)
            {
                if (TryBuildPath(target.transform, stage.prefabContentsRoot.transform, out objectPath) == false)
                {
                    error = "Object is outside the prefab being edited.";
                    return false;
                }

                assetPath = stage.assetPath;
                isPrefabAsset = true;
                return true;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(target))
            {
                var root = target.transform.root;
                assetPath = AssetDatabase.GetAssetPath(root.gameObject);
                isPrefabAsset = true;
                return TryBuildPath(target.transform, root, out objectPath);
            }

            if (target.scene.IsValid() == false || string.IsNullOrEmpty(target.scene.path))
            {
                error = "Object is not part of a saved scene or prefab.";
                return false;
            }

            assetPath = target.scene.path;
            return TryBuildPath(target.transform, null, out objectPath);
        }

        public static HierarchyBindingsTarget Resolve(HierarchyBindingsRequest request, out string error)
        {
            error = string.Empty;

            if (request.IsPrefabAsset)
                return ResolvePrefab(request, out error);

            return ResolveScene(request, out error);
        }

        public void Commit()
        {
            if (_loadedContents != null)
            {
                PrefabUtility.SaveAsPrefabAsset(_loadedContents, _assetPath);
                return;
            }

            if (_scene.IsValid())
                EditorSceneManager.MarkSceneDirty(_scene);
        }

        public void Dispose()
        {
            if (_loadedContents == null)
                return;

            PrefabUtility.UnloadPrefabContents(_loadedContents);
            _loadedContents = null;
        }

        private static HierarchyBindingsTarget ResolvePrefab(HierarchyBindingsRequest request, out string error)
        {
            error = string.Empty;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage != null && stage.assetPath == request.AssetPath)
            {
                var staged = FindChild(stage.prefabContentsRoot.transform, request.ObjectPath);

                if (staged == null)
                {
                    error = $"'{request.ObjectPath}' is missing in the open prefab stage.";
                    return null;
                }

                // Открытую сцену стадии сохраняет сам Unity, от нас нужна только пометка dirty.
                return new HierarchyBindingsTarget
                {
                    Root = staged.gameObject,
                    _assetPath = request.AssetPath,
                    _scene = stage.scene
                };
            }

            var contents = PrefabUtility.LoadPrefabContents(request.AssetPath);

            if (contents == null)
            {
                error = $"Failed to open prefab {request.AssetPath}.";
                return null;
            }

            var found = FindChild(contents.transform, request.ObjectPath);

            if (found == null)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                error = $"'{request.ObjectPath}' is missing in {request.AssetPath}.";
                return null;
            }

            return new HierarchyBindingsTarget
            {
                Root = found.gameObject,
                _assetPath = request.AssetPath,
                _loadedContents = contents
            };
        }

        private static HierarchyBindingsTarget ResolveScene(HierarchyBindingsRequest request, out string error)
        {
            error = string.Empty;

            var scene = SceneManager.GetSceneByPath(request.AssetPath);

            if (scene.IsValid() == false || scene.isLoaded == false)
            {
                error = $"Scene {request.AssetPath} is not open.";
                return null;
            }

            var separator = request.ObjectPath.IndexOf('/');
            var rootName = separator < 0 ? request.ObjectPath : request.ObjectPath.Substring(0, separator);
            var rest = separator < 0 ? string.Empty : request.ObjectPath.Substring(separator + 1);

            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                if (sceneRoot.name != rootName)
                    continue;

                var found = FindChild(sceneRoot.transform, rest);

                if (found == null)
                    continue;

                return new HierarchyBindingsTarget
                {
                    Root = found.gameObject,
                    _assetPath = request.AssetPath,
                    _scene = scene
                };
            }

            error = $"'{request.ObjectPath}' is missing in {request.AssetPath}.";
            return null;
        }

        // Путь строится от указанного корня; для сцены корень не задан и путь идёт до самого верха.
        private static bool TryBuildPath(Transform target, Transform root, out string path)
        {
            path = string.Empty;

            var names = new List<string>();
            var current = target;

            while (current != null)
            {
                if (current == root)
                {
                    names.Reverse();
                    path = string.Join("/", names);
                    return true;
                }

                names.Add(current.name);
                current = current.parent;
            }

            if (root != null)
                return false;

            names.Reverse();
            path = string.Join("/", names);
            return true;
        }

        private static Transform FindChild(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            return root.Find(path);
        }
    }
}