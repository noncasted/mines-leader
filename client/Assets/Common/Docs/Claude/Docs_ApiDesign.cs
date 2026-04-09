/// <summary>
/// AI REFERENCE: API Design Patterns
///
/// Guidelines for designing clean, flexible APIs using UniTask, IReadOnlyList, and error handling.
/// Used for: async methods, collections, file operations, external integrations.
///
/// Read: /projects/storytale-workshop/client/docs/API_DESIGN.md
/// </summary>

using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Docs.Claude
{
    public class Docs_ApiDesign
    {
        // ==================== ASYNC METHODS ====================

        // Example: UniTask async method naming
        // UniTask<T> returns without Async suffix (unlike Task<T>)
        public async UniTask<IReadOnlyList<Sprite>> LoadSprites(string title = "Select Sprites")
        {
            // Demonstrate async load
            await UniTask.Delay(100);
            return new List<Sprite>();
        }

        // Example: Async method with proper return type
        public async UniTask<string> ProcessFile(string path)
        {
            await UniTask.Delay(50);
            return File.Exists(path) ? "success" : "not found";
        }

        // ==================== COLLECTION RETURN TYPES ====================

        // Example: IReadOnlyList for flexible API
        // Client doesn't care if it's List, Array, or custom collection
        public IReadOnlyList<int> GetItemIds()
        {
            var items = new List<int> { 1, 2, 3, 4, 5 };
            return items; // Can return List, Array, or any IReadOnlyList implementation
        }

        // Example: Using IReadOnlyList in client code
        public void ProcessItems()
        {
            var itemIds = GetItemIds();

            // All these work with IReadOnlyList
            foreach (var id in itemIds)
            {
            } // Iteration

            var first = itemIds[0]; // Indexing
            var count = itemIds.Count; // Count property

            // Implementation can change (List -> Array -> custom) without breaking client
        }

        // ==================== EMPTY COLLECTIONS ====================

        // Example: Return empty collection instead of null
        public IReadOnlyList<Sprite> SafeLoadSprites(bool cancelled)
        {
            if (cancelled)
            {
                return Array.Empty<Sprite>(); // Safe - foreach works on empty
            }

            var sprites = new List<Sprite> { };
            return sprites;
        }

        // Example: Client code - no null checks needed
        public void DisplaySprites()
        {
            var sprites = SafeLoadSprites(false);

            // Safe to iterate - empty collection doesn't throw
            foreach (var sprite in sprites)
            {
                // Display sprite
            }

            // Safe to check count
            if (sprites.Count == 0)
            {
                // Show "no sprites" message
            }
        }

        // ==================== FILE OPERATIONS ====================

        // Example: Safe sprite loading from file path
        private static Sprite LoadSpriteFromPath(string path)
        {
            try
            {
                // 1. Validate extension
                var extension = Path.GetExtension(path).ToLowerInvariant();
                var validExtensions = new[] { ".png", ".jpg", ".jpeg" };

                if (Array.IndexOf(validExtensions, extension) < 0)
                {
                    return null; // Unsupported format
                }

                // 2. Read binary data
                var imageData = File.ReadAllBytes(path);
                var texture = new Texture2D(1, 1);

                // 3. Load into texture
                if (!texture.LoadImage(imageData))
                {
                    Object.Destroy(texture); // Full namespace (avoid conflict with object)
                    return null;
                }

                // 4. Create sprite from texture
                var sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        Vector2.one * 0.5f, // Pivot at center
                        100f // PixelsPerUnit
                    );

                return sprite;
            }
            catch
            {
                // Graceful degradation - catch all errors
                // File may be corrupted, locked, inaccessible
                return null;
            }
        }

        // Example: Load multiple sprites with error handling
        public IReadOnlyList<Sprite> LoadSpritesFromPaths(IReadOnlyList<string> paths)
        {
            var loadedSprites = new List<Sprite>();

            foreach (var path in paths)
            {
                var sprite = LoadSpriteFromPath(path);

                if (sprite != null)
                {
                    loadedSprites.Add(sprite); // Only add successful loads
                }
                // Failed loads are skipped silently
            }

            return loadedSprites;
        }

        // ==================== FILE BROWSER INTEGRATION ====================

        // Example: Callback-based async pattern
        public async UniTask<IReadOnlyList<Sprite>> LoadSpritesWithFileBrowser(string title)
        {
            var selectedPaths = new List<string>();
            var cancelled = false;

            // FileBrowser uses callbacks, not returns
            var dialogResult = SimpleFileBrowser.FileBrowser.ShowLoadDialog(
                onSuccess: paths => {
                    selectedPaths.AddRange(paths); // Callback populates list
                },
                onCancel: () => {
                    cancelled = true; // Flag for cancellation
                },
                pickMode: SimpleFileBrowser.FileBrowser.PickMode.Files,
                allowMultiSelection: true,
                title: title);

            // If dialog failed to open - return empty
            if (!dialogResult)
            {
                return Array.Empty<Sprite>();
            }

            // Wait for callback to execute
            while (selectedPaths.Count == 0 && !cancelled)
            {
                await UniTask.Delay(16); // Check every frame (~60fps)
            }

            // Check result
            if (cancelled || selectedPaths.Count == 0)
            {
                return Array.Empty<Sprite>();
            }

            // Load sprites from selected paths
            return LoadSpritesFromPaths(selectedPaths);
        }

        // ==================== PATTERNS ====================

        // Pattern: Wrapping callback-based API with UniTask
        public async UniTask<T> WrapCallbackAsync<T>(
            Func<Action<T>, Action, bool> callbackApi,
            string description)
        {
            var result = default(T);
            var completed = false;
            var cancelled = false;

            var dialogResult = callbackApi(
                value => {
                    result = value;
                    completed = true;
                },
                () => {
                    cancelled = true;
                });

            if (!dialogResult)
            {
                throw new InvalidOperationException($"Failed to show {description} dialog");
            }

            while (!completed && !cancelled)
            {
                await UniTask.Delay(16);
            }

            if (cancelled)
            {
                throw new OperationCanceledException($"{description} was cancelled");
            }

            return result;
        }

        // Pattern: Async with timeout
        public async UniTask<T> LoadWithTimeout<T>(
            Func<UniTask<T>> loader,
            int timeoutMs = 5000)
        {
            try
            {
                var loadTask = loader();
                var timeoutTask = UniTask.Delay(timeoutMs);

                var (_, completedTask) = await UniTask.WhenAny(loadTask, timeoutTask);

                if (completedTask == null) // timeout completed first
                {
                    throw new TimeoutException($"Load operation exceeded {timeoutMs}ms");
                }

                return await loadTask;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Load failed: {ex.Message}");
                throw;
            }
        }

        // ==================== RULES ====================

        // RULE 1: UniTask<T> without Async suffix
        // ✅ Correct - UniTask doesn't need Async suffix
        public async UniTask<int> CalculateAsync()
        {
            await UniTask.Delay(10);
            return 42;
        }

        // RULE 2: Return IReadOnlyList not Array
        // ✅ Correct - flexible contract
        public IReadOnlyList<string> GetNamesCorrect()
        {
            return new List<string> { "Alice", "Bob" };
        }

        // ❌ Wrong - rigid Array contract
        public string[] GetNamesWrong()
        {
            return new[] { "Alice", "Bob" };
        }

        // RULE 3: Empty collection not null
        public IReadOnlyList<int> GetEmptyCorrect()
        {
            return Array.Empty<int>(); // ✅ Safe for foreach
        }

        public int[] GetEmptyWrong()
        {
            return null; // ❌ Requires null check in client
        }

        // RULE 4: File errors return null with logging
        private static Texture2D LoadTextureWithErrorHandling(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var data = File.ReadAllBytes(path);
                var texture = new Texture2D(1, 1);

                if (!texture.LoadImage(data))
                {
                    Object.Destroy(texture); // Cleanup on error
                    return null;
                }

                return texture;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load texture: {ex.Message}");
                return null;
            }
        }

        // RULE 5: Always use full UnityEngine.Object name
        private static void CleanupResource(Object resource)
        {
            if (resource != null)
            {
                Object.Destroy(resource); // ✅ Full name (avoids conflict with 'object')
                // Object.Destroy(resource); // ❌ Wrong - conflicts with C# 'object' type
            }
        }

        // RULE 6: Callback to UniTask pattern
        public async UniTask<bool> WaitForDialogueComplete()
        {
            var isComplete = false;
            var wasCancelled = false;

            // Subscribe with callbacks
            var result = true; // Assume dialog showed

            while (!isComplete && !wasCancelled)
            {
                await UniTask.Delay(16); // Poll every frame
            }

            return isComplete;
        }

        // ==================== HELPER CLASSES ====================

        private class SimpleFileBrowser
        {
            public class FileBrowser
            {
                public enum PickMode
                {
                    Files,
                    Folders,
                    FilesAndFolders
                }

                public static bool ShowLoadDialog(
                    Action<string[]> onSuccess,
                    Action onCancel,
                    PickMode pickMode = PickMode.Files,
                    bool allowMultiSelection = false,
                    string title = "Select")
                {
                    return true; // Mock implementation
                }
            }
        }
    }
}