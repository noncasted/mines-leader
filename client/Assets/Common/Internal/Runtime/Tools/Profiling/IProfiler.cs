using System;
using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    /// Замерщик этапов загрузки. Живёт один на трассу: создаётся в начале замеряемого
    /// процесса, отдаёт корневые скоупы и на <see cref="Finish"/> складывает результат
    /// в <see cref="ProfilerTraceStorage"/>, откуда его читает окно водопада.
    /// </summary>
    public interface IProfiler
    {
        string Name { get; }

        /// <summary>Время от старта профайлера, мс. Единая шкала для всех скоупов трассы.</summary>
        double ElapsedMs { get; }

        /// <summary>Кадров с начала трассы.</summary>
        int ElapsedFrames { get; }

        /// <summary>Создаёт корневой скоуп. Запускать его нужно вручную — <see cref="IProfilerScope.Start"/>.</summary>
        IProfilerScope CreateScope(string name);

        /// <summary>Останавливает трассу, дописывает незакрытые скоупы и сохраняет результат.</summary>
        void Finish();
    }

    /// <summary>
    /// Отрезок на шкале трассы. Dispose эквивалентен <see cref="Stop"/> — удобно для using.
    /// </summary>
    public interface IProfilerScope : IDisposable
    {
        string Name { get; }
        bool IsRunning { get; }

        /// <summary>Начало отрезка от старта профайлера, мс. -1, пока скоуп не стартовал.</summary>
        double StartMs { get; }

        /// <summary>Длительность отрезка, мс. Для работающего скоупа — время с его старта.</summary>
        double DurationMs { get; }

        /// <summary>
        /// Сколько кадров занял отрезок. Асинхронная загрузка почти всегда упирается
        /// в кадры, а не в процессор, и по этому числу это сразу видно.
        /// </summary>
        int Frames { get; }

        IReadOnlyList<IProfilerScope> Children { get; }

        IProfilerScope Child(string name);

        /// <summary>
        /// Переименовать отрезок: имя загруженной сцены или ассета известно только
        /// после того, как загрузка закончилась, а скоуп нужен до её старта.
        /// </summary>
        void SetName(string name);

        void Start();
        void Stop();
    }
}