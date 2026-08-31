using System;
using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    /// Плоское представление трассы для сериализации: JsonUtility не умеет рекурсивные
    /// структуры, поэтому дерево хранится списком с ссылками на родителя.
    /// </summary>
    [Serializable]
    public class ProfilerTraceData
    {
        public string Name;

        /// <summary>Метка времени в имени файла. По ней рядом с трассой ищется дамп кадров.</summary>
        public string Id;

        public string StartedAt;
        public double DurationMs;
        public int Frames;

        /// <summary>Первый и последний кадр движка, которые заняла трасса. Абсолютные.</summary>
        public int StartFrame;
        public int EndFrame;

        /// <summary>Границы трассы по часам профайлера, нс.</summary>
        public long StartNs;
        public long EndNs;

        public bool Cold;
        public List<ProfilerSpanData> Spans = new();
    }

    [Serializable]
    public class ProfilerSpanData
    {
        public int Id;
        public int ParentId;
        public int Depth;
        public string Name;
        public double StartMs;
        public double DurationMs;
        public int Frames;

        /// <summary>Кадры движка, которые занял отрезок.</summary>
        public int StartFrame;
        public int EndFrame;

        /// <summary>Границы отрезка по часам профайлера, нс: по ним поднимаются сэмплы его кадров.</summary>
        public long StartNs;
        public long EndNs;

        public bool Unfinished;

        public double EndMs => StartMs + DurationMs;
    }
}
