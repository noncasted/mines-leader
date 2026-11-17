using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Internal
{
    // Where a handler that threw is reported: a listener of a lifetime that ends, a handler of an event source. Every
    // handler of the pass runs before the report, so one failing handler does not keep the others from running
    // (listeners of a lifetime release resources). The Unity client installs Debug.LogException (ReactiveLogging of
    // the core); without a handler the exception is rethrown after the pass, several of them as an AggregateException.
    public static class ReactiveExceptions
    {
        public static Action<Exception> Handler { get; private set; }

        // Null removes the handler.
        public static void SetHandler(Action<Exception> handler)
        {
            Handler = handler;
        }

        // Public for the other passes that follow the same rule (the updater of the client core).
        public static void Report(List<Exception> exceptions)
        {
            var handler = Handler;

            if (handler == null)
            {
                if (exceptions.Count == 1)
                    ExceptionDispatchInfo.Capture(exceptions[0]).Throw();

                throw new AggregateException(exceptions);
            }

            foreach (var exception in exceptions)
                handler.Invoke(exception);
        }
    }
}
