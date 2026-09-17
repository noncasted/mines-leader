using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Настройки импорта задаются папкой: ручные правки в инспекторе перезапишутся при реимпорте.
    public sealed class AudioImportPostprocessor : AssetPostprocessor
    {
        private const string MusicFolder = "Assets/Art/Audio/Music/";
        private const string SoundsFolder = "Assets/Art/Audio/Sounds/";

        private void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;

            if (assetPath.StartsWith(MusicFolder))
            {
                // Decompress On Load распаковал бы весь трек в PCM, поэтому музыка остаётся сжатой в памяти.
                Apply(importer, false, new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.CompressedInMemory,
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    quality = 0.45f,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                });
            }
            else if (assetPath.StartsWith(SoundsFolder))
            {
                // Короткие эффекты распакованы заранее, чтобы играть без задержки.
                Apply(importer, true, new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    quality = 0.5f,
                    sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate,
                    sampleRateOverride = 22050,
                });
            }
        }

        private static void Apply(AudioImporter importer, bool forceToMono, AudioImporterSampleSettings settings)
        {
            importer.forceToMono = forceToMono;
            importer.loadInBackground = true;

            // На WebGL Play() не догружает клип сам и молчит: данные грузятся вместе с ассетом из бандла.
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;

            // Оверрайды платформ перекрыли бы общие настройки.
            importer.ClearSampleSettingOverride("WebGL");
            importer.ClearSampleSettingOverride("Standalone");
        }
    }
}
