using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Трассы кладём на диск: замер идёт в плей-моде, а смотрим мы его в окне редактора
    /// уже после выхода, когда доменные объекты уже мертвы.
    /// </summary>
    [NoAutoStaticsCleanup]
    public static class ProfilerTraceStorage
    {
        private const int MaxTraces = 20;

        public static event Action<ProfilerTraceData> Saved;

        public static ProfilerTraceData Last { get; private set; }

        /// <summary>Файл последней сохранённой трассы. Нужен редакторному сбору кадров: он дописывает дамп рядом.</summary>
        public static string LastPath { get; private set; }

        /// <summary>
        /// Запуск идёт на свежеперекомпилированных сборках и сброшенном кеше ассетов.
        /// Ставится редакторным хуком, снимается после первой же сохранённой трассы.
        /// </summary>
        public static bool ColdDomain { get; set; }

        /// <summary>
        /// Трассы лежат рядом с проектом, в client/traces: смотреть их удобнее там же,
        /// где лежит код, а не в persistentDataPath. В плеере проектной папки нет —
        /// там падаем обратно в persistentDataPath.
        /// </summary>
        public static string Directory
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "traces"));
#else
                return Path.Combine(Application.persistentDataPath, "ProfilerTraces");
#endif
            }
        }

        /// <summary>
        /// Где окно ищет трассы. В редакторе это и папка проекта, и каталог плеера:
        /// persistentDataPath в редакторе указывает ровно туда же, куда пишет билд с тем
        /// же company/product, так что трассы из билда видно в том же окне.
        /// </summary>
        public static IReadOnlyList<string> SearchDirectories
        {
            get
            {
#if UNITY_EDITOR
                return new[]
                {
                    Directory,
                    Path.Combine(Application.persistentDataPath, "ProfilerTraces")
                };
#else
                return new[] { Directory };
#endif
            }
        }

        public static void Save(ProfilerTraceData trace)
        {
            Last = trace;
            LastPath = null;
            ColdDomain = false;

            try
            {
                var directory = Directory;

                if (System.IO.Directory.Exists(directory) == false)
                    System.IO.Directory.CreateDirectory(directory);

                trace.Id = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

                var name = $"trace_{trace.Id}.json";
                var path = Path.Combine(directory, name);
                var json = JsonUtility.ToJson(trace);

                File.WriteAllText(path, json);

                LastPath = path;

                Cleanup(directory);

#if !UNITY_EDITOR
                // В билде окна редактора нет, а на WebGL до файла в IndexedDB не добраться
                // руками — поэтому трасса дублируется в лог: в десктопном билде её видно
                // в Player.log, в браузере — в консоли.
                Debug.Log($"[Profiler] Trace saved to {path}");
                Debug.Log($"[Profiler] Trace json: {json}");
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiler] Failed to save trace: {e.Message}");
            }

            Saved?.Invoke(trace);
        }

        public static IReadOnlyList<string> List()
        {
            var result = new List<string>();

            foreach (var directory in SearchDirectories)
            {
                if (System.IO.Directory.Exists(directory) == false)
                    continue;

                result.AddRange(System.IO.Directory.GetFiles(directory, "trace_*.json"));
            }

            // Имя файла — метка времени, так что свежие оказываются сверху независимо
            // от того, из какого каталога они пришли.
            result.Sort((left, right) => string.CompareOrdinal(
                Path.GetFileName(right),
                Path.GetFileName(left)));

            return result;
        }

        public static ProfilerTraceData Load(string path)
        {
            try
            {
                return JsonUtility.FromJson<ProfilerTraceData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiler] Failed to read trace {path}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Дамп сэмплов профайлера лежит рядом с трассой и зовётся по её метке времени:
        /// он тяжёлый, и таскать его внутри самой трассы незачем — окно читает его только
        /// тогда, когда в скоуп реально заглянули.
        /// </summary>
        public static string FramesPath(string tracePath)
        {
            if (string.IsNullOrEmpty(tracePath))
                return null;

            var directory = Path.GetDirectoryName(tracePath);
            var name = Path.GetFileNameWithoutExtension(tracePath);

            if (string.IsNullOrEmpty(directory) || name.StartsWith("trace_") == false)
                return null;

            return Path.Combine(directory, $"frames_{name.Substring("trace_".Length)}.json");
        }

        /// <summary>Чистим только свои трассы, каталог плеера трогать не за чем.</summary>
        public static void Clear()
        {
            if (System.IO.Directory.Exists(Directory) == false)
                return;

            foreach (var file in System.IO.Directory.GetFiles(Directory, "trace_*.json"))
                File.Delete(file);

            foreach (var file in System.IO.Directory.GetFiles(Directory, "frames_*.json"))
                File.Delete(file);

            Last = null;
            LastPath = null;
        }

        private static void Cleanup(string directory)
        {
            var files = System.IO.Directory.GetFiles(directory, "trace_*.json");

            if (files.Length <= MaxTraces)
                return;

            Array.Sort(files, StringComparer.Ordinal);

            for (var i = 0; i < files.Length - MaxTraces; i++)
            {
                var frames = FramesPath(files[i]);

                File.Delete(files[i]);

                if (string.IsNullOrEmpty(frames) == false && File.Exists(frames))
                    File.Delete(frames);
            }
        }
    }
}