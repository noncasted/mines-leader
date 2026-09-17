using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [InitializeOnLoad]
    public static class AudioCatalogInspector
    {
        static AudioCatalogInspector()
        {
            Editor.finishedDefaultHeaderGUI += Draw;
        }

        private static void Draw(Editor editor)
        {
            if (CatalogInspectorGUI.TryCollectImporters(editor, IsCatalogTarget, out var importers) == false)
                return;

            using (CatalogInspectorGUI.BeginSection())
            {
                var states = ResolveStates(importers);
                DrawIncluded(importers, states);

                if (states.Included == false || states.IncludedMixed)
                    return;

                CatalogGroupField.Draw(
                    AudioGroupsRegistry.Instance,
                    states.Group,
                    states.GroupMixed,
                    group => Apply(importers, metadata => metadata.Group = group));

                DrawVolume(importers, states);
            }
        }

        private static bool IsCatalogTarget(AssetImporter importer)
        {
            return importer is AudioImporter && AudioCatalogGenerator.IsSourcePath(importer.assetPath);
        }

        private static CatalogStates ResolveStates(IReadOnlyList<AssetImporter> importers)
        {
            var first = AudioCatalogMetadata.ReadOrDefault(importers[0]);
            var includedMixed = false;
            var groupMixed = false;
            var volumeMixed = false;

            for (var i = 1; i < importers.Count; i++)
            {
                var metadata = AudioCatalogMetadata.ReadOrDefault(importers[i]);

                if (metadata.Included != first.Included)
                    includedMixed = true;

                if (string.Equals(metadata.Group, first.Group, StringComparison.Ordinal) == false)
                    groupMixed = true;

                if (Mathf.Approximately(metadata.Volume, first.Volume) == false)
                    volumeMixed = true;
            }

            return new CatalogStates(
                first.Included,
                first.Group,
                first.Volume,
                includedMixed,
                groupMixed,
                volumeMixed);
        }

        private static void DrawIncluded(IReadOnlyList<AssetImporter> importers, CatalogStates states)
        {
            if (CatalogInspectorGUI.TryDrawToggle("Audio Catalog", states.Included, states.IncludedMixed,
                out var included))
                Apply(importers, metadata => metadata.Included = included);
        }

        private static void DrawVolume(IReadOnlyList<AssetImporter> importers, CatalogStates states)
        {
            var changed = CatalogInspectorGUI.TryDraw(
                states.VolumeMixed,
                () => EditorGUILayout.Slider("Volume", states.Volume, 0f, 1f),
                out var volume);

            if (changed)
                Apply(importers, metadata => metadata.Volume = volume);
        }

        private static void Apply(IReadOnlyList<AssetImporter> importers, Action<AudioCatalogMetadata> mutate)
        {
            foreach (var importer in importers)
            {
                Undo.RecordObject(importer, "Audio Catalog");
                var metadata = AudioCatalogMetadata.ReadOrDefault(importer);
                mutate(metadata);
                AudioCatalogMetadata.Write(importer, metadata);
            }

            // Генератор в плеймоде ждёт выхода, а громкость хочется подбирать на слух прямо в игре.
            if (EditorApplication.isPlaying)
                AudioCatalogGenerator.SyncVolumes(importers);

            AudioCatalogGenerator.ScheduleGenerate();
        }

        private readonly struct CatalogStates
        {
            public CatalogStates(
                bool included,
                string group,
                float volume,
                bool includedMixed,
                bool groupMixed,
                bool volumeMixed)
            {
                Included = included;
                Group = group;
                Volume = volume;
                IncludedMixed = includedMixed;
                GroupMixed = groupMixed;
                VolumeMixed = volumeMixed;
            }

            public bool Included { get; }
            public string Group { get; }
            public float Volume { get; }
            public bool IncludedMixed { get; }
            public bool GroupMixed { get; }
            public bool VolumeMixed { get; }
        }
    }
}
