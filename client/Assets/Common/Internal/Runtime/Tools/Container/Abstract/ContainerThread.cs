using System;
using System.Threading;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class ContainerThread
    {
        public static void Assert()
        {
#if UNITY_EDITOR || DEBUG
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                throw new InvalidOperationException("Container is main-thread only.");
            }
#endif
        }

#if UNITY_EDITOR || DEBUG
        private static int _mainThreadId = Thread.CurrentThread.ManagedThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CaptureMainThread()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }
#endif
    }
}