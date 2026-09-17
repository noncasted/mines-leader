using System;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Генераторы каталогов запускаются из редакторских событий пачками, поэтому запуск
    // защищён от реентерабельности, откладывается на время импорта/компиляции и дебаунсится.
    public sealed class CatalogGenerationRunner
    {
        private const float DebounceSeconds = 0.5f;

        private readonly string _logTag;
        private readonly Action _generate;

        private bool _isGenerating;
        private bool _isScheduled;
        private bool _isWaitingPlayMode;
        private double _scheduledTime;

        public CatalogGenerationRunner(string logTag, Action generate)
        {
            _logTag = logTag;
            _generate = generate;
        }

        public void Run()
        {
            if (_isGenerating)
                return;

            // В плеймоде ассеты не перегенерируются: запуск переносится на выход из него,
            // иначе правки разметки во время игры теряются.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                WaitPlayModeExit();
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Schedule();
                return;
            }

            if (EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            _isGenerating = true;

            try
            {
                _generate();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{_logTag}] Generate failed: {exception}");
            }
            finally
            {
                _isGenerating = false;
            }
        }

        private void WaitPlayModeExit()
        {
            if (_isWaitingPlayMode)
                return;

            _isWaitingPlayMode = true;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _isWaitingPlayMode = false;
            Run();
        }

        public void RunDelayed()
        {
            EditorApplication.delayCall += Run;
        }

        public void Schedule()
        {
            _scheduledTime = EditorApplication.timeSinceStartup + DebounceSeconds;

            if (_isScheduled)
                return;

            _isScheduled = true;
            EditorApplication.update += TickSchedule;
        }

        private void TickSchedule()
        {
            if (EditorApplication.timeSinceStartup < _scheduledTime)
                return;

            EditorApplication.update -= TickSchedule;
            _isScheduled = false;
            Run();
        }
    }
}