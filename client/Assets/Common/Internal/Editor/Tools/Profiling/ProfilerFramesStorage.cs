using System.IO;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Запись дампа кадров. Дамп читают не в редакторе, а снаружи — по нему разбирают,
    /// на что ушло время внутри скоупов трассы, поэтому окну он не нужен и в него не грузится.
    /// </summary>
    public static class ProfilerFramesStorage
    {
        public static void Save(string path, ProfilerFramesData frames)
        {
            var directory = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, JsonUtility.ToJson(frames));
        }
    }
}