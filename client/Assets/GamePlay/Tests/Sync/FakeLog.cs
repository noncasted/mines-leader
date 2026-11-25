using System;
using System.Collections.Generic;
using Internal;

namespace GamePlay.Tests
{
    // A log that keeps what was written: the tests check the errors of a service without the Unity console.
    public class FakeLog<T> : ILog<T>
    {
        private readonly List<(LogLevel Level, string Message, Exception Exception)> _entries = new();

        public string Category => typeof(T).FullName;
        public IReadOnlyList<(LogLevel Level, string Message, Exception Exception)> Entries => _entries;

        public bool IsEnabled(LogLevel level) => true;

        public void Write(LogLevel level, string template, string message, Exception exception)
        {
            _entries.Add((level, message, exception));
        }
    }
}
