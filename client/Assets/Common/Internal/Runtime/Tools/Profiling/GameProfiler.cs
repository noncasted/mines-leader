using System;
using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    /// Точка доступа к трассе загрузки. Этапы загрузки размазаны по статическим
    /// расширениям скоупов, куда профайлер через DI не протащить, поэтому текущая
    /// трасса живёт статикой, а вложенность держится стеком открытых скоупов.
    ///
    /// Стек корректен, пока цепочка ожиданий линейна. Ветки, которые уходят в
    /// параллель (fire-and-forget), поднимаются через <see cref="Branch"/> — они
    /// ложатся в корень трассы и на чужую вложенность не влияют.
    /// </summary>
    public static class GameProfiler
    {
        private static readonly List<IProfilerScope> _stack = new();

        public static IProfiler Current { get; private set; }

        public static bool IsRunning => Current != null;

        public static IProfiler Begin(string name)
        {
            _stack.Clear();
            Current = new Profiler(name);

            return Current;
        }

        public static void Finish()
        {
            if (Current == null)
                return;

            _stack.Clear();
            Current.Finish();
            Current = null;
        }

        /// <summary>Скоуп внутри текущего открытого этапа.</summary>
        public static IProfilerScope Scope(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            var parent = _stack.Count > 0 ? _stack[^1] : null;
            var scope = parent == null ? Current.CreateScope(name) : parent.Child(name);

            return Open(scope);
        }

        /// <summary>
        /// Отрезок, который идёт параллельно соседям: вкладывается в текущий этап, но сам
        /// на стек не встаёт. Иначе соседняя загрузка, стартовавшая тем же WhenAll, вложилась
        /// бы в него и трасса врала бы про причинность.
        /// </summary>
        public static IProfilerScope Concurrent(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            var parent = _stack.Count > 0 ? _stack[^1] : null;
            var scope = parent == null ? Current.CreateScope(name) : parent.Child(name);

            scope.Start();

            return scope;
        }

        /// <summary>
        /// Отрезок, открытый на текущем этапе. Нужен коду, который замеряет свои шаги уже
        /// после первого await: стека там нет, а родителя взять неоткуда.
        /// </summary>
        public static IProfilerScope CurrentScope => _stack.Count > 0 ? _stack[^1] : NullProfilerScope.Instance;

        /// <summary>
        /// Делает уже открытый отрезок текущим. Нужен для параллельных веток: пока идёт их
        /// синхронный пролог, вложенные замеры должны попадать в свою ветку, а не в соседнюю.
        /// </summary>
        public static IDisposable Ambient(IProfilerScope scope)
        {
            if (Current == null || scope is NullProfilerScope)
                return NullAmbient.Instance;

            _stack.Add(scope);

            return new AmbientHandle(scope);
        }

        /// <summary>Скоуп в корне трассы: для веток, которые идут параллельно основной цепочке.</summary>
        public static IProfilerScope Branch(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            return Open(Current.CreateScope(name));
        }

        /// <summary>
        /// Корневой скоуп, который не попадает в стек: замеряет отрезок, внутри которого
        /// чужой код не должен вкладываться.
        /// </summary>
        public static IProfilerScope Detached(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            var scope = Current.CreateScope(name);
            scope.Start();

            return scope;
        }

        private static IProfilerScope Open(IProfilerScope scope)
        {
            scope.Start();
            _stack.Add(scope);

            return new AmbientScope(scope);
        }

        private static void Close(IProfilerScope scope)
        {
            var index = _stack.LastIndexOf(scope);

            if (index >= 0)
                _stack.RemoveAt(index);
        }

        /// <summary>Снимает отрезок со стека, не останавливая его: останавливает его владелец.</summary>
        private class AmbientHandle : IDisposable
        {
            public AmbientHandle(IProfilerScope scope)
            {
                _scope = scope;
            }

            private readonly IProfilerScope _scope;

            public void Dispose()
            {
                Close(_scope);
            }
        }

        private class NullAmbient : IDisposable
        {
            public static readonly NullAmbient Instance = new();

            public void Dispose() { }
        }

        /// <summary>
        /// Обёртка, которая на Dispose снимает скоуп со стека. Снимаем по ссылке, а не
        /// с вершины: параллельные ветки закрываются не в том порядке, в каком открывались.
        /// </summary>
        private class AmbientScope : IProfilerScope
        {
            public AmbientScope(IProfilerScope scope)
            {
                _scope = scope;
            }

            private readonly IProfilerScope _scope;

            public string Name => _scope.Name;
            public bool IsRunning => _scope.IsRunning;
            public double StartMs => _scope.StartMs;
            public double DurationMs => _scope.DurationMs;
            public int Frames => _scope.Frames;
            public IReadOnlyList<IProfilerScope> Children => _scope.Children;

            public IProfilerScope Child(string name) => _scope.Child(name);

            public void SetName(string name) => _scope.SetName(name);

            public void Start() => _scope.Start();

            public void Stop()
            {
                _scope.Stop();
                Close(_scope);
            }

            public void Dispose()
            {
                Stop();
            }
        }
    }

    public class NullProfilerScope : IProfilerScope
    {
        public static readonly NullProfilerScope Instance = new();

        private static readonly IProfilerScope[] _children = new IProfilerScope[0];

        public string Name => string.Empty;
        public bool IsRunning => false;
        public double StartMs => 0d;
        public double DurationMs => 0d;
        public int Frames => 0;
        public IReadOnlyList<IProfilerScope> Children => _children;

        public IProfilerScope Child(string name) => Instance;

        public void SetName(string name) { }

        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
