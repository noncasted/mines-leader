using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Достаёт из Unity-профайлера сэмплы тех кадров, которые заняла трасса, и кладёт их
    /// рядом с ней отдельным файлом. Работает только в редакторе: покадровые данные живут
    /// в кольцевом буфере профайлера, до которого из рантайма не дотянуться.
    ///
    /// Профайлер нумерует кадры своим счётчиком, не совпадающим с <see cref="Time.frameCount"/>,
    /// поэтому кадры ищутся по времени — см. <see cref="TryResolveRange"/>.
    /// </summary>
    [InitializeOnLoad]
    [NoAutoStaticsCleanup]
    public static class ProfilerFrameCapture
    {
        private const string EnabledKey = "Internal.Profiler.CaptureFrames";

        /// <summary>Главный поток. Загрузка живёт на нём, а дамп всех потоков раздувает файл на порядок.</summary>
        private const int MainThreadIndex = 0;

        private const double MinSampleMs = 0.1d;
        private const int MaxDepth = 32;
        private const int MaxSamplesPerFrame = 1500;
        private const int MaxFrames = 900;

        /// <summary>
        /// Общий потолок на дамп. Кадров в загрузке за тысячу, и без него один запуск
        /// с deep profiling кладёт в traces сотню мегабайт.
        /// </summary>
        private const int MaxTotalSamples = 150_000;

        /// <summary>Сколько замеров сдвига держим, чтобы взять медиану и не поехать на случайном.</summary>
        private const int OffsetSamples = 32;

        /// <summary>
        /// Сколько тиков редактора ждём, пока профайлер долистает хвост трассы. Последние кадры
        /// загрузки попадают в буфер уже после того, как трасса закрылась, а самые тяжёлые из
        /// них — как раз хвостовые.
        /// </summary>
        private const int TailTicks = 30;

        private static readonly List<int> _offsets = new();

        private static ProfilerTraceData _pending;
        private static string _pendingPath;
        private static int _pendingTicks;

        static ProfilerFrameCapture()
        {
            ProfilerDriver.NewProfilerFrameRecorded += OnProfilerFrameRecorded;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ProfilerTraceStorage.Saved += OnTraceSaved;

            StartRecording();
        }

        /// <summary>
        /// Сбор кадров стоит включённого профайлера на весь запуск, поэтому его можно выключить:
        /// когда нужен только водопад скоупов, платить за запись сэмплов незачем.
        /// </summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            _offsets.Clear();

            StartRecording();
        }

        /// <summary>
        /// Запись профайлера держим поднятой заранее, ещё в эдит-моде: если включать её на
        /// входе в плей-мод, первые кадры запуска — самые тяжёлые — в буфер не попадают,
        /// профайлер поднимается пару кадров. Это и есть плата за включённый тумблер Frames.
        /// </summary>
        private static void StartRecording()
        {
            if (Enabled && ProfilerDriver.enabled == false)
                ProfilerDriver.enabled = true;
        }

        private static void OnProfilerFrameRecorded(int connectionId, int frameIndex)
        {
            if (EditorApplication.isPlaying == false)
                return;

            _offsets.Add(frameIndex - Time.frameCount);

            if (_offsets.Count > OffsetSamples)
                _offsets.RemoveAt(0);
        }

        /// <summary>
        /// Снимать кадры прямо здесь нельзя: трасса закрывается посреди кадра, и ни он, ни
        /// пара предыдущих в буфер профайлера ещё не легли. Поэтому ждём, пока профайлер
        /// долистает до конца трассы, и только потом читаем.
        /// </summary>
        private static void OnTraceSaved(ProfilerTraceData trace)
        {
            if (Enabled == false)
                return;

            var path = ProfilerTraceStorage.FramesPath(ProfilerTraceStorage.LastPath);

            if (string.IsNullOrEmpty(path))
                return;

            _pending = trace;
            _pendingPath = path;
            _pendingTicks = 0;

            EditorApplication.update -= WaitForTail;
            EditorApplication.update += WaitForTail;
        }

        private static void WaitForTail()
        {
            _pendingTicks++;

            if (_pendingTicks < TailTicks && HasTail(_pending) == false)
                return;

            EditorApplication.update -= WaitForTail;

            var trace = _pending;
            var path = _pendingPath;

            _pending = null;
            _pendingPath = null;

            if (trace == null || string.IsNullOrEmpty(path))
                return;

            try
            {
                var frames = Capture(trace);
                ProfilerFramesStorage.Save(path, frames);

                Debug.Log($"[Profiler] Captured {frames.Frames.Count} profiler frames" +
                          (frames.MissedFrames > 0 ? $", {frames.MissedFrames} missed" : string.Empty) +
                          $" -> {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiler] Failed to capture profiler frames: {e.Message}");
            }
        }

        /// <summary>Последний записанный кадр перекрыл конец трассы — ждать больше нечего.</summary>
        private static bool HasTail(ProfilerTraceData trace)
        {
            if (trace == null || trace.EndNs <= 0L)
                return false;

            return TryGetBounds(ProfilerDriver.lastFrameIndex, out _, out var endNs) && endNs >= trace.EndNs;
        }

        public static ProfilerFramesData Capture(ProfilerTraceData trace)
        {
            var frames = new ProfilerFramesData
            {
                TraceName = trace.Name,
                TraceId = trace.Id,
                FirstFrame = trace.StartFrame,
                LastFrame = trace.EndFrame,
                DeepProfiling = ProfilerDriver.deepProfiling,
                MinSampleMs = MinSampleMs,
                ThreadName = "Main Thread"
            };

            if (TryResolveRange(trace, out var first, out var last, out var frameOffset) == false)
            {
                frames.Warning = "Профайлер не писал кадры этого запуска: включи запись в окне Profiler.";
                return frames;
            }

            var baseStartMs = double.NaN;
            var samples = new List<ProfilerSampleData>();
            var captured = 0;

            for (var profilerFrame = first; profilerFrame <= last; profilerFrame++)
            {
                if (frames.Frames.Count >= MaxFrames)
                {
                    frames.Warning = $"Кадров больше {MaxFrames}, дамп обрезан.";
                    break;
                }

                if (captured >= MaxTotalSamples)
                {
                    frames.Warning = $"Сэмплов больше {MaxTotalSamples}, дамп обрезан.";
                    break;
                }

                var data = CaptureFrame(profilerFrame + frameOffset, profilerFrame, samples);

                if (data == null)
                    continue;

                if (double.IsNaN(baseStartMs))
                    baseStartMs = data.StartMs;

                data.StartMs -= baseStartMs;
                captured += data.Samples.Count;

                frames.Frames.Add(data);
            }

            frames.MissedFrames = Math.Max(0, trace.EndFrame - trace.StartFrame + 1 - frames.Frames.Count);

            // Дыры считаем по времени, а не по числу кадров: у начала и у хвоста причины
            // разные, и чинятся они тоже по-разному.
            if (frames.Frames.Count > 0 && trace.StartNs > 0L)
            {
                frames.HeadGapMs = Math.Max(0d, (frames.Frames[0].StartNs - trace.StartNs) / 1e6d);
                frames.TailGapMs = Math.Max(0d, (trace.EndNs - frames.Frames[^1].EndNs) / 1e6d);
            }

            frames.Warning = Describe(frames);

            return frames;
        }

        /// <summary>
        /// Чего в дампе не хватает. Молча обрезанный дамп хуже отсутствующего: по нему
        /// делают вывод, что тяжёлого в загрузке нет, а тяжёлое просто не записалось.
        /// </summary>
        private static string Describe(ProfilerFramesData frames)
        {
            if (string.IsNullOrEmpty(frames.Warning) == false)
                return frames.Warning;

            if (frames.Frames.Count == 0)
                return "Кадров трассы в буфере профайлера не нашлось.";

            var parts = new List<string>();

            if (frames.HeadGapMs > 1d)
            {
                parts.Add($"первые {frames.HeadGapMs:F0} мс запуска не записаны — профайлер поднимается " +
                          "пару кадров; чтобы попали, запись должна быть включена ещё до входа в плей-мод");
            }

            if (frames.TailGapMs > 1d)
                parts.Add($"последние {frames.TailGapMs:F0} мс не записаны — профайлер не успел долистать хвост");

            if (frames.DeepProfiling == false)
                parts.Add("Deep Profiling выключён: в дереве только маркеры движка, без методов скриптов");

            return string.Join("; ", parts);
        }

        /// <summary>
        /// Кадры профайлера, попавшие в трассу, и сдвиг их номеров к кадрам движка.
        ///
        /// Считаем по часам профайлера: его нумерация кадров идёт от старта редактора и
        /// стоит, пока запись выключена, поэтому разница между ней и <see cref="Time.frameCount"/>
        /// не постоянна, а старые кадры прошлой записи лежат в буфере как ни в чём не бывало.
        /// Время же у обеих сторон одно, и по нему привязка точная.
        /// </summary>
        private static bool TryResolveRange(ProfilerTraceData trace, out int first, out int last, out int frameOffset)
        {
            first = 0;
            last = -1;
            frameOffset = 0;

            var oldest = ProfilerDriver.firstFrameIndex;
            var newest = ProfilerDriver.lastFrameIndex;

            if (newest < oldest)
                return false;

            if (trace.StartNs <= 0L || trace.EndNs <= trace.StartNs)
                return TryResolveByFrameCount(trace, oldest, newest, out first, out last, out frameOffset);

            for (var frame = newest; frame >= oldest; frame--)
            {
                if (TryGetBounds(frame, out var startNs, out var endNs) == false)
                    continue;

                // Кадр целиком позже трассы: до её конца ещё не дошли.
                if (startNs > trace.EndNs)
                    continue;

                if (last < 0)
                    last = frame;

                if (endNs < trace.StartNs)
                    break;

                first = frame;

                // Кадр, в котором трасса стартовала, — это её первый кадр движка.
                if (startNs <= trace.StartNs)
                {
                    frameOffset = trace.StartFrame - frame;
                    return true;
                }
            }

            if (last < 0)
                return false;

            // Начало трассы уже вытеснено из буфера — тогда якорь по концу.
            frameOffset = trace.EndFrame - last;

            return true;
        }

        /// <summary>
        /// Запасной путь для трасс без часов профайлера: из билда или снятых до того, как
        /// время начали писать. Сдвиг тут приблизительный — колбэк профайлера приходит
        /// с опозданием на кадр-другой, — поэтому и сходится он не всегда.
        /// </summary>
        private static bool TryResolveByFrameCount(ProfilerTraceData trace, int oldest, int newest, out int first, out int last, out int frameOffset)
        {
            first = 0;
            last = -1;
            frameOffset = 0;

            if (TryGetOffset(out var offset) == false)
                return false;

            first = Math.Max(oldest, trace.StartFrame + offset);
            last = Math.Min(newest, trace.EndFrame + offset);
            frameOffset = -offset;

            return last >= first;
        }

        private static bool TryGetBounds(int profilerFrame, out long startNs, out long endNs)
        {
            startNs = 0L;
            endNs = 0L;

            RawFrameDataView view = null;

            try
            {
                view = ProfilerDriver.GetRawFrameDataView(profilerFrame, MainThreadIndex);

                if (view == null || view.valid == false)
                    return false;

                startNs = (long)view.frameStartTimeNs;
                endNs = startNs + (long)view.frameTimeNs;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                view?.Dispose();
            }
        }

        private static ProfilerFrameData CaptureFrame(int frame, int profilerFrame, List<ProfilerSampleData> samples)
        {
            HierarchyFrameDataView view = null;

            try
            {
                view = ProfilerDriver.GetHierarchyFrameDataView(
                    profilerFrame,
                    MainThreadIndex,
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    HierarchyFrameDataView.columnTotalTime,
                    false);

                if (view == null || view.valid == false)
                    return null;

                var root = view.GetRootItemID();

                var data = new ProfilerFrameData
                {
                    Frame = frame,
                    ProfilerFrame = profilerFrame,
                    StartMs = view.frameStartTimeMs,
                    StartNs = (long)view.frameStartTimeNs,
                    EndNs = (long)(view.frameStartTimeNs + view.frameTimeNs),
                    DurationMs = view.frameTimeMs,
                    GcAlloc = (long)view.GetItemColumnDataAsDouble(root, HierarchyFrameDataView.columnGcMemory)
                };

                samples.Clear();
                Append(view, root, -1, samples, out var truncated);

                data.Samples = new List<ProfilerSampleData>(samples);
                data.Truncated = truncated;

                return data;
            }
            finally
            {
                view?.Dispose();
            }
        }

        /// <summary>
        /// Обход дерева кадра в глубину. Корень в дамп не пишем: это служебный узел вида
        /// «весь кадр», его цифры уже есть в <see cref="ProfilerFrameData.DurationMs"/>.
        /// </summary>
        private static void Append(HierarchyFrameDataView view, int id, int depth, List<ProfilerSampleData> samples, out bool truncated)
        {
            truncated = false;

            if (depth >= 0)
            {
                if (samples.Count >= MaxSamplesPerFrame)
                {
                    truncated = true;
                    return;
                }

                samples.Add(new ProfilerSampleData
                {
                    Depth = depth,
                    Name = view.GetItemName(id),
                    TotalMs = view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnTotalTime),
                    SelfMs = view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnSelfTime),
                    Calls = (int)view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls),
                    GcAlloc = (long)view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnGcMemory)
                });
            }

            if (depth + 1 >= MaxDepth || view.HasItemChildren(id) == false)
                return;

            // Список детей общий для всего обхода не сделать: рекурсия затрёт его на первом же
            // ребёнке, поэтому уровень держит свою копию.
            var children = new List<int>();
            view.GetItemChildren(id, children);

            foreach (var child in children)
            {
                // Порог отсечки: без него один кадр с deep profiling — это десятки тысяч
                // сэмплов вида «List`1.get_Count() 0.00ms», в которых ничего не видно.
                if (view.GetItemColumnDataAsDouble(child, HierarchyFrameDataView.columnTotalTime) < MinSampleMs)
                    continue;

                Append(view, child, depth + 1, samples, out var childTruncated);
                truncated |= childTruncated;
            }
        }

        /// <summary>
        /// Сдвиг между <see cref="Time.frameCount"/> и счётчиком кадров профайлера. Берём медиану
        /// накопленных замеров: единичный замер может съехать на кадр, если колбэк профайлера
        /// пришёл уже после того, как движок начал следующий кадр.
        /// </summary>
        private static bool TryGetOffset(out int offset)
        {
            offset = 0;

            if (_offsets.Count == 0)
                return false;

            var sorted = new List<int>(_offsets);
            sorted.Sort();

            offset = sorted[sorted.Count / 2];

            return true;
        }
    }
}
