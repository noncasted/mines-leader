/// <summary>
/// AI REFERENCE: Code Style Guide
///
/// Conventions for member organization, naming, method structure, and error handling.
/// Applied to: all classes, especially services and managers.
///
/// Read: .claude/docs/CODE_STYLE.md (also in .claude/rules/CODE_STYLE.md)
/// </summary>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Docs.Claude
{
    public class Docs_CodeStyle
    {
        // ==================== MEMBER ORGANIZATION ====================

        // Example: Correct member order - MANDATORY
        public class Example_MemberOrganization
        {
            // 1. Constructor FIRST
            public Example_MemberOrganization(ILogger<Example_MemberOrganization> logger)
            {
                _logger = logger;
            }

            // 2. Private fields (readonly grouped first, then mutable)
            private readonly ILogger<Example_MemberOrganization> _logger;
            private readonly Dictionary<string, object> _cache = new();
            private int _accessCount = 0;

            // 3. Public methods (interface implementation)
            public void ProcessData(string key)
            {
                _accessCount++;
            }

            // 4. Private methods
            private void LogAccess()
            {
                // _logger.Log($"Access count: {_accessCount}");
            }

            // 5. Local functions (inside methods)
            public void ComplexOperation()
            {
                var data = GetData();

                void LogOperation()
                {
                    // Log with captured 'data'
                }

                LogOperation();
            }

            private object GetData() => new object();
        }

        // ==================== FIELD NAMING ====================

        // Example: Correct field naming conventions
        public class Example_FieldNaming
        {
            // ✅ CORRECT - clear, descriptive, leading underscore

            private readonly Dictionary<string, object> _delegates = new();
            private readonly Dictionary<string, object> _observers = new();
            private readonly List<Func<Task>> _resubscribeActions = new();
            private readonly ILogger<Example_FieldNaming> _logger = null;

            private int _accessCount = 0;
            private bool _isInitialized = false;

            // ❌ WRONG - abbreviated, unclear
            // private readonly Dictionary<string, object> _d = new();
            // private readonly List<Func<Task>> _actions = new();
            // private int _count = 0;
        }

        // ==================== METHOD LOGIC ORGANIZATION ====================

        // Example: Logical structure of method - 7 steps
        public class Example_MethodLogic
        {
            private readonly Dictionary<string, object> _cache = new();
            private readonly ILogger<Example_MethodLogic> _logger = null;

            public object GetOrCreateItem(string key)
            {
                // 1. Fast path - check if already exists
                if (_cache.TryGetValue(key, out var existing))
                {
                    return existing;
                }

                // 2. Creation - instantiate new object
                var newItem = new object();

                // 3. Setup - initialize and register
                var config = CreateConfig(key);

                // 4. Setup verification - ensure persistence
                GC.KeepAlive(newItem);

                // 5. Side effects - add to collections, register callbacks
                _cache[key] = newItem;

                // 6. Return
                return newItem;

                // 7. Local functions - helpers at end
                object CreateConfig(string k)
                {
                    return new object();
                }
            }
        }

        // ==================== LOCAL FUNCTIONS ====================

        // Example: When to use local functions
        public class Example_LocalFunctions
        {
            private ILogger<Example_LocalFunctions> _logger = null;

            // ✅ CORRECT - local function with closure
            public async Task ProcessData()
            {
                var data = GetData();

                // Local function captures 'data' without parameters
                Task Subscribe()
                {
                    // Use 'data' from outer scope
                    return LogDataAsync(data);
                }

                await Subscribe();
            }

            // ❌ WRONG - extracting to separate method loses context
            public async Task ProcessData_Wrong()
            {
                var data = GetData();
                // Would need to pass 'data' as parameter to separate method
                // Loses the natural grouping of related logic
            }

            private object GetData() => new object();

            private async Task LogDataAsync(object data)
            {
                await Task.Delay(10);
            }
        }

        // ==================== EXCEPTION HANDLING ====================

        // Example: Safe subscribe pattern
        public class Example_ExceptionHandling
        {
            private ILogger<Example_ExceptionHandling> _logger = null;

            public async Task SubscribeWithFallback()
            {
                // Local function with try-catch
                Task Subscribe()
                {
                    try
                    {
                        // Attempt to subscribe
                        return Task.Delay(10);
                    }
                    catch (Exception ex)
                    {
                        // Log with context
                        _logger?.Log($"Subscribe failed: {ex.Message}");

                        // Graceful degradation - don't throw
                        return Task.CompletedTask;
                    }
                }

                await Subscribe();
            }

            // ✅ CORRECT - graceful degradation
            private async Task SafeOperation()
            {
                try
                {
                    await Task.Delay(10);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Operation failed: {ex.Message}");
                    // Continue execution - don't throw
                }
            }

            // ❌ WRONG - throws and stops execution
            private async Task AggressiveOperation()
            {
                try
                {
                    await Task.Delay(10);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Operation failed", ex);
                }
            }
        }

        // ==================== LOGGING ====================

        // Example: Structured logging with tags
        public class Example_Logging
        {
            private ILogger<Example_Logging> _logger = null;

            public void LogMessage()
            {
                // ✅ CORRECT - with tags and structure
                _logger?.Log("[Messaging] [Queue] Failed to rebind observer");

                var queueId = "queue-123";
                var error = new Exception("Timeout");

                _logger?.Log($"[Messaging] [Queue] Failed to rebind queue {queueId}: {error.Message}");
            }

            public void LogError(Exception ex, string queueId)
            {
                // ✅ CORRECT format with context
                // _logger.LogError(ex, "[Messaging] [Queue] Failed to rebind observer to queue {QueueId}", queueId);

                // Structure: exception, format string with tags, parameters
            }

            // ❌ WRONG - no context, no tags
            public void LogError_Wrong(Exception ex)
            {
                // _logger.LogError("Error occurred");
            }
        }

        // ==================== USING .NoAwait() ====================

        // Example: Fire-and-forget with NoAwait
        public class Example_NoAwait
        {
            public void FireAndForget()
            {
                // ✅ CORRECT - explicitly shows intent
                Task Task1() => Task.Delay(10);

                // Task1().NoAwait();  // Explicitly ignores result

                // ✅ Good for background operations
                // var resubscribeTask = Subscribe();
                // resubscribeTask.NoAwait();
            }

            // ❌ WRONG - silent result discard
            public void FireAndForget_Wrong()
            {
                // Task.Delay(10);  // Result is discarded silently
            }
        }

        // ==================== COLLECTION INITIALIZATION ====================

        // Example: Inline collection initialization
        public class Example_CollectionInit
        {
            // ✅ CORRECT - clearly empty on declaration
            private readonly Dictionary<string, object> _cache = new();
            private readonly List<int> _items = new();

            // ❌ WRONG - must search constructor for initialization
            // private readonly Dictionary<string, object> _cache;
            // public Example_CollectionInit() {
            //     _cache = new Dictionary<string, object>();
            // }
        }

        // ==================== COLLECTION MODIFICATION ====================

        // Example: Safe iteration with deferred removal
        public class Example_IterationModification
        {
            private List<int> _items = new();

            // ✅ CORRECT - collect IDs, remove after iteration
            public void RemoveCompleted()
            {
                var toRemove = new List<int>();

                foreach (var item in _items)
                {
                    if (item < 0)
                    {
                        toRemove.Add(item);
                    }
                }

                foreach (var item in toRemove)
                {
                    _items.Remove(item);
                }
            }

            // ❌ WRONG - backward iteration complexity
            public void RemoveCompleted_Wrong()
            {
                // for (var i = _items.Count - 1; i >= 0; i--)
                // {
                //     if (_items[i] < 0)
                //         _items.RemoveAt(i);
                // }
            }
        }

        // ==================== DICTIONARY LOOKUP ====================

        // Example: TryGetValue pattern
        public class Example_DictionaryLookup
        {
            private Dictionary<string, object> _cache = new();

            // ✅ CORRECT - single lookup
            public object Get(string key)
            {
                if (_cache.TryGetValue(key, out var value))
                {
                    return value;
                }

                return null;
            }

            // ❌ WRONG - two lookups
            public object Get_Wrong(string key)
            {
                if (_cache.ContainsKey(key))
                {
                    // return _cache[key];  // Second lookup!
                }

                return null;
            }
        }

        // ==================== GC.KeepAlive ====================

        // Example: GC.KeepAlive usage
        public class Example_GCKeepAlive
        {
            private Dictionary<string, object> _observers = new();

            public void RegisterObserver(string key)
            {
                var observer = new object();

                // Store reference
                _observers[key] = observer;

                // ✅ CORRECT - prevents GC collection
                GC.KeepAlive(observer);

                // Without this, GC might collect observer before callbacks execute
            }
        }

        // ==================== BRACES PLACEMENT ====================

        // Example: Correct brace placement - MANDATORY
        public class Example_BracesPlacement
        {
            // ✅ CORRECT - opening brace on SAME line
            public void Method1()
            {
                // ...
            }

            public class InnerClass
            {
                public void InnerMethod()
                {
                    // ...
                }
            }

            // ❌ WRONG - brace on new line (violates EditorConfig)
            // public void Method2()
            // {
            //     // ...
            // }
        }

        // ==================== COMPLETE SERVICE TEMPLATE ====================

        // Example: Full service class template
        public class Example_ServiceTemplate
        {
            // 1. Constructor
            public Example_ServiceTemplate(ILogger<Example_ServiceTemplate> logger)
            {
                _logger = logger;
            }

            // 2. Private fields (readonly first, then mutable)
            private readonly ILogger<Example_ServiceTemplate> _logger;
            private readonly Dictionary<string, object> _state = new();
            private int _operationCount = 0;

            // 3. Public methods (interface)
            public void Execute(string key)
            {
                _operationCount++;

                if (_state.TryGetValue(key, out var value))
                {
                    ProcessValue(value);
                }
            }

            public int GetOperationCount() => _operationCount;

            // 4. Private methods
            private void ProcessValue(object value)
            {
                // Implementation
            }

            private void LogState()
            {
                // Implementation
            }
        }
    }

    // ==================== INTERFACE FOR LOGGING ====================

    public interface ILogger<T>
    {
        void Log(string message);
    }
}