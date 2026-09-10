using System;
using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    /// Сэмплы Unity-профайлера по кадрам, которые заняла трасса. Лежат отдельным файлом
    /// рядом с трассой: сама трасса — это десяток килобайт, а дамп кадров тяжёлый, и
    /// тащить его в каждый скоуп незачем — скоуп хранит только номера своих кадров.
    /// </summary>
    [Serializable]
    public class ProfilerFramesData
    {
        public string TraceName;
        public string TraceId;

        /// <summary>Диапазон кадров движка, который пытались собрать.</summary>
        public int FirstFrame;

        public int LastFrame;

        /// <summary>Собранные кадры отсортированы по номеру, но дырки в них возможны.</summary>
        public List<ProfilerFrameData> Frames = new();

        /// <summary>Кадры, которых в кольцевом буфере профайлера уже не было.</summary>
        public int MissedFrames;

        /// <summary>Сколько миллисекунд трассы не попало в дамп с начала и с конца.</summary>
        public double HeadGapMs;

        public double TailGapMs;

        /// <summary>Без deep profiling в дереве только маркеры движка, без методов скриптов.</summary>
        public bool DeepProfiling;

        public string ThreadName;

        /// <summary>Порог отсечки в мс: всё, что короче, в дамп не попало.</summary>
        public double MinSampleMs;

        public string Warning;
    }

    [Serializable]
    public class ProfilerFrameData
    {
        /// <summary>Кадр движка, тот же счётчик, что в <see cref="ProfilerSpanData.StartFrame"/>.</summary>
        public int Frame;

        /// <summary>Индекс кадра внутри профайлера: по нему кадр открывается в окне Profiler.</summary>
        public int ProfilerFrame;

        /// <summary>Начало кадра от начала первого собранного кадра, мс.</summary>
        public double StartMs;

        /// <summary>Границы кадра по часам профайлера, нс: по ним кадр ложится на отрезки трассы.</summary>
        public long StartNs;

        public long EndNs;

        public double DurationMs;
        public long GcAlloc;

        /// <summary>Иерархия сэмплов кадра, плоским списком с глубиной — как и спаны трассы.</summary>
        public List<ProfilerSampleData> Samples = new();

        /// <summary>Дерево кадра упёрлось в лимит сэмплов и обрезано.</summary>
        public bool Truncated;
    }

    [Serializable]
    public class ProfilerSampleData
    {
        public int Depth;
        public string Name;
        public double TotalMs;
        public double SelfMs;
        public int Calls;
        public long GcAlloc;
    }
}