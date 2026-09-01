using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class CardIconsExporter
    {
        [Serializable]
        private class CardInfo
        {
            public string type;
            public string name;
            public string description;
            public string icon;
        }

        [Serializable]
        private class CardsInfoRoot
        {
            public CardInfo[] cards;
        }

        private static readonly string[] IconExtensions = { ".png", ".psd", ".aseprite" };

        public static void Export()
        {
            string jsonPath = Path.Combine(Application.dataPath, "Common", "Resources", "cards-info.json");

            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[CardIconsExporter] cards-info.json not found at: {jsonPath}");
                return;
            }

            string json = File.ReadAllText(jsonPath);
            var root = JsonUtility.FromJson<CardsInfoRoot>(json);

            if (root?.cards == null || root.cards.Length == 0)
            {
                Debug.LogWarning("[CardIconsExporter] No cards found in JSON.");
                return;
            }

            string projectRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName)?.FullName;
            string outputDir = Path.Combine(projectRoot, "docs", "obsidian", "game", "cards", "icons");

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            int exported = 0;
            int skipped = 0;

            foreach (var card in root.cards)
            {
                if (string.IsNullOrEmpty(card.icon))
                {
                    Debug.LogWarning($"[CardIconsExporter] Card '{card.type}' has no icon path.");
                    skipped++;
                    continue;
                }

                string assetPath = FindIconAssetPath(card.icon);

                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogWarning($"[CardIconsExporter] Icon not found for card '{card.type}' (searched: {card.icon}).");
                    skipped++;
                    continue;
                }

                Sprite sprite = LoadSprite(assetPath);

                if (sprite == null)
                {
                    Debug.LogWarning($"[CardIconsExporter] Failed to load sprite for card '{card.type}' at: {assetPath}");
                    skipped++;
                    continue;
                }

                Texture2D readable = MakeReadable(sprite);
                byte[] bytes = readable.EncodeToPNG();
                Object.DestroyImmediate(readable);

                string outputPath = Path.Combine(outputDir, $"{card.type}.png");
                File.WriteAllBytes(outputPath, bytes);
                exported++;
            }

            Debug.Log($"[CardIconsExporter] Done. Exported: {exported}, Skipped: {skipped}. Output: {outputDir}");
            AssetDatabase.Refresh();
        }

        private static string FindIconAssetPath(string iconRelativePath)
        {
            foreach (string ext in IconExtensions)
            {
                string path = $"Assets/Common/Resources/{iconRelativePath}{ext}";
                if (File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar))))
                    return path;
            }

            return null;
        }

        private static Sprite LoadSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                return sprite;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null)
            {
                var rect = new Rect(0, 0, tex.width, tex.height);
                return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f));
            }

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var obj in allAssets)
            {
                if (obj is Sprite s)
                    return s;
            }

            return null;
        }

        private static Texture2D MakeReadable(Sprite sprite)
        {
            var sourceTex = sprite.texture;
            var rect = sprite.rect;

            RenderTexture rt = RenderTexture.GetTemporary(sourceTex.width, sourceTex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(sourceTex, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D readable = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.ARGB32, false);
            readable.ReadPixels(new Rect(rect.x, rect.y, rect.width, rect.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            return readable;
        }
    }
}
