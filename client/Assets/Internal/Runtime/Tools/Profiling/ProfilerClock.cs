namespace Internal
{
    /// <summary>
    /// Часы Unity-профайлера. По ним отрезки трассы кладутся на те же кадры, которые
    /// профайлер записал у себя: номера кадров с двух сторон не сходятся (профайлер
    /// считает свои и не считает те, что записаны не были), а время — общее.
    /// </summary>
    public static class ProfilerClock
    {
        /// <summary>Текущее время часов профайлера в наносекундах. 0, если профайлера в сборке нет.</summary>
        public static long NowNs()
        {
#if ENABLE_PROFILER
            var ratio = Unity.Profiling.LowLevel.Unsafe.ProfilerUnsafeUtility.TimestampToNanosecondsConversionRatio;
            var timestamp = Unity.Profiling.LowLevel.Unsafe.ProfilerUnsafeUtility.Timestamp;

            if (ratio.Denominator <= 0L)
                return 0L;

            // В лоб timestamp * Numerator переполняет long: тики считаются от старта машины,
            // поэтому множим остаток отдельно.
            return timestamp / ratio.Denominator * ratio.Numerator +
                   timestamp % ratio.Denominator * ratio.Numerator / ratio.Denominator;
#else
            return 0L;
#endif
        }
    }
}
