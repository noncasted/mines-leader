/// <summary>
/// AI REFERENCE: Dependency Injection Container & Scope Lifecycle
///
/// VContainer-based DI with event-driven scope initialization lifecycle.
/// Scope initialization order: BaseSetup → Setup → SetupCompletion → Loaded → Dispose.
/// Used for: service registration, MonoBehaviour injection, scope setup callbacks.
///
/// Read: .claude/docs/COMMON_CONTAINER.md
/// </summary>

using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using VContainer;

namespace Docs.Claude
{
    public class Docs_Container
    {
        // ==================== SCOPE LIFECYCLE ====================

        // Example: Scope initialization flow
        // Order: BaseSetup → Setup → SetupCompletion → Loaded → Dispose
        public class Example_ScopeSetupFlow :
            IScopeBaseSetup,
            IScopeBaseSetupAsync,
            IScopeSetup,
            IScopeSetupAsync,
            IScopeSetupCompletion,
            IScopeSetupCompletionAsync,
            IScopeLoaded,
            IScopeLoadedAsync,
            IScopeDispose,
            IScopeDisposeAsync
        {
            private int _initOrder = 0;

            // 1. BaseSetup - earliest sync phase
            public void OnBaseSetup(IReadOnlyLifetime lifetime)
            {
                _initOrder = 1; // First
            }

            // 2. BaseSetupAsync - earliest async phase
            public UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime)
            {
                // Can load resources async
                return UniTask.Delay(10);
            }

            // 3. Setup - sync initialization phase
            public void OnSetup(IReadOnlyLifetime lifetime)
            {
                _initOrder = 3; // After BaseSetup/BaseSetupAsync
                // Initialize state, register listeners
            }

            // 4. SetupAsync - main async initialization
            public UniTask OnSetupAsync(IReadOnlyLifetime lifetime)
            {
                // Load main assets, connect to services
                return UniTask.Delay(10);
            }

            // 5. SetupCompletion - completion sync phase
            public void OnSetupCompletion(IReadOnlyLifetime lifetime)
            {
                _initOrder = 5; // After Setup/SetupAsync
                // Final initialization, show UI, start game
            }

            // 6. SetupCompletionAsync - completion async phase
            public UniTask OnSetupCompletionAsync(IReadOnlyLifetime lifetime)
            {
                // Final async work
                return UniTask.Delay(10);
            }

            // 7. Loaded - called after all initialization complete
            public void OnLoaded(IReadOnlyLifetime lifetime)
            {
                _initOrder = 7; // Scope fully loaded
            }

            // 8. LoadedAsync - async loaded phase
            public UniTask OnLoadedAsync(IReadOnlyLifetime lifetime)
            {
                return UniTask.Delay(10);
            }

            // 9. Dispose - cleanup sync
            public void OnDispose()
            {
                _initOrder = 0; // Clean up
            }

            // 10. DisposeAsync - cleanup async
            public UniTask OnDisposeAsync()
            {
                return UniTask.Delay(10);
            }
        }

        // ==================== SCENE SERVICE ====================

        // Example: ISceneService pattern
        // Used by MonoBehaviour components that manage scene initialization
        public interface IMyGameService
        {
            void Initialize();
        }

        public class Example_ISceneService : Example_MonoBehaviourService, ISceneService
        {
            // ISceneService.Create() is called during scope registration
            public void Create(IScopeBuilder builder)
            {
                // Register this component as service
                builder.RegisterComponent(this)
                       .As<IMyGameService>();

                // Register other dependencies
                // builder.RegisterSingleton<ILogger, ConsoleLogger>();
            }
        }

        // ==================== MONOBEHAVIOUR SERVICE ====================

        // Example: MonoBehaviour service pattern (MANDATORY)
        public class Example_MonoBehaviourService : MonoBehaviour,
                                                    ISceneService,
                                                    IScopeSetup
        {
            private ILogger<Example_MonoBehaviourService> _logger;

            // Called by DI container when scope is created
            [Inject]
            private void Construct(ILogger<Example_MonoBehaviourService> logger)
            {
                _logger = logger;
            }

            // ISceneService - register this component with scope
            public void Create(IScopeBuilder builder)
            {
                builder.RegisterComponent(this)
                       .As<IMyGameService>();
            }

            // IScopeSetup - initialize when scope is set up
            public void OnSetup(IReadOnlyLifetime lifetime)
            {
                // Initialize with scope lifetime
                // Subscribe to events, start coroutines, etc
                lifetime.Listen(() => Debug.Log("Scope terminating"));
            }

            private class ILogger<T>
            {
            }
        }

        // ==================== VIEW INJECTOR ====================

        // Example: IViewInjector for MonoBehaviour dependencies
        public class Example_ViewInjector
        {
            private IViewInjector _injector;

            public void InjectMonoBehaviour()
            {
                var go = new GameObject("MyView");
                var component = go.AddComponent<ViewComponent>();

                // Inject dependencies into MonoBehaviour
                _injector.Inject(component);

                // Component now has dependencies resolved
            }

            public void InjectGameObject()
            {
                var go = new GameObject("MyGameObject");

                // Inject all MonoBehaviours in GameObject
                _injector.Inject(go);

                // All MonoBehaviours with [Inject] now have dependencies
            }
        }

        public class ViewComponent : MonoBehaviour
        {
            private ILogger<ViewComponent> _logger;

            // [Inject] called by IViewInjector.Inject()
            [Inject]
            private void Construct(ILogger<ViewComponent> logger)
            {
                _logger = logger;
            }

            private class ILogger<T>
            {
            }
        }

        // ==================== SCOPE BUILDER EXTENSIONS ====================

        // Example: AddViewEvents - connects MonoBehaviour to lifecycle
        public class Example_ScopeBuilderUsage
        {
            public void RegisterWithLifecycle(IScopeBuilder builder, MonoBehaviour target)
            {
                // Register component
                builder.RegisterComponent(target)
                       .As<ISceneService>();

                // Connect to scope lifecycle events:
                // - If implements IScopeBaseSetup → OnBaseSetup() called
                // - If implements IScopeSetup → OnSetup() called
                // - If implements IScopeDispose → OnDispose() called
                builder.AddViewEvents(target);
            }
        }

        // ==================== PATTERNS ====================

        // Pattern: Scope lifecycle with async operations
        public class Pattern_AsyncLifecycle :
            IScopeSetupAsync,
            IScopeDisposeAsync
        {
            public async UniTask OnSetupAsync(IReadOnlyLifetime lifetime)
            {
                // Async resource loading
                await UniTask.Delay(100);

                // Subscribe with lifetime for auto-cleanup
                lifetime.Listen(() => Debug.Log("Scope terminating"));
            }

            public async UniTask OnDisposeAsync()
            {
                // Async cleanup
                await UniTask.Delay(100);
            }
        }

        // Pattern: Service dependencies with lifetime
        public class Pattern_DependencyWithLifetime :
            IScopeSetup
        {
            public void OnSetup(IReadOnlyLifetime lifetime)
            {
                // Lifetime valid for this scope
                // Auto-cleanup when scope terminates
            }
        }

        // Pattern: Event loop phases
        public class Pattern_EventLoopPhases
        {
            // EventLoop calls these in order:
            // 1. RunConstructSync/RunConstruct (during scope create)
            //    - OnBaseSetup()
            //    - OnBaseSetupAsync()
            //    - OnSetup()
            //    - OnSetupAsync()
            //    - OnSetupCompletion()
            //    - OnSetupCompletionAsync()
            //
            // 2. RunLoadedSync/RunLoaded (after scene load)
            //    - OnLoaded()
            //    - OnLoadedAsync()
            //
            // 3. RunDispose (when scope terminates)
            //    - OnDispose()
            //    - OnDisposeAsync()
        }

        // ==================== RULES ====================

        // RULE 1: ISceneService.Create() must register component
        // ✅ Correct - registers self with scope
        public class Rule1_ServiceRegistration : MonoBehaviour, ISceneService
        {
            public void Create(IScopeBuilder builder)
            {
                builder.RegisterComponent(this)
                       .As<IMyGameService>();
            }
        }

        // ❌ Wrong - doesn't register anything
        public class Rule1_ServiceRegistration_Wrong : ISceneService
        {
            public void Create(IScopeBuilder builder)
            {
                // Does nothing - component won't be injected
            }
        }

        // RULE 2: Scope lifetime valid during scope lifecycle
        public class Rule2_LifetimeValidity :
            IScopeSetup,
            IScopeDispose
        {
            public void OnSetup(IReadOnlyLifetime lifetime)
            {
                // ✅ Correct - lifetime valid here
                lifetime.Listen(() => Debug.Log("Scope ending"));
            }

            public void OnDispose()
            {
                // ❌ Wrong - cannot get lifetime in Dispose
                // Scope is already terminating
            }
        }

        // RULE 3: BaseSetup before Setup before SetupCompletion
        // Event loop guarantees this order:
        // OnBaseSetup() → OnSetup() → OnSetupCompletion()
        public class Rule3_InitializationOrder :
            IScopeBaseSetup,
            IScopeSetup,
            IScopeSetupCompletion
        {
            private bool _baseSetupDone = false;
            private bool _setupDone = false;

            public void OnBaseSetup(IReadOnlyLifetime lifetime)
            {
                _baseSetupDone = true;
                // First phase
            }

            public void OnSetup(IReadOnlyLifetime lifetime)
            {
                // ✅ _baseSetupDone is guaranteed to be true
                if (!_baseSetupDone)
                    throw new InvalidOperationException();

                _setupDone = true;
            }

            public void OnSetupCompletion(IReadOnlyLifetime lifetime)
            {
                // ✅ Both BaseSetup and Setup are guaranteed to be done
                if (!_setupDone)
                    throw new InvalidOperationException();
            }
        }

        // RULE 4: Use IViewInjector for MonoBehaviour injection
        public class Rule4_ViewInjector
        {
            // ✅ Correct - let container inject dependencies
            public class ViewWithDependencies : MonoBehaviour
            {
                private ILogger<ViewWithDependencies> _logger;

                [Inject]
                private void Construct(ILogger<ViewWithDependencies> logger)
                {
                    _logger = logger; // Injected by container
                }

                private class ILogger<T>
                {
                }
            }

            // ❌ Wrong - manual instantiation loses DI
            public void WrongWay()
            {
                // var view = new ViewWithDependencies();
                // _logger would be null!
            }
        }

        // RULE 5: AddViewEvents connects lifecycle to MonoBehaviour
        public class Rule5_AddViewEvents : IScopeSetup
        {
            public void OnSetup(IReadOnlyLifetime lifetime)
            {
                var mb = new MonoBehaviour(); // hypothetical

                // ✅ Correct - connects to lifetime-based lifecycle
                // When called with AddViewEvents, lifecycle methods are invoked
            }
        }

        // ==================== EVENT LOOP EXAMPLE ====================

        // Example: How EventLoop coordinates initialization
        public class Example_EventLoopFlow
        {
            // Pseudo-code of EventLoop.RunConstruct:
            //
            // 1. Invoke all IScopeBaseSetup.OnBaseSetup()
            // 2. Await all IScopeBaseSetupAsync.OnBaseSetupAsync()
            // 3. Invoke all IScopeSetup.OnSetup()
            // 4. Await all IScopeSetupAsync.OnSetupAsync()
            // 5. Invoke all IScopeSetupCompletion.OnSetupCompletion()
            // 6. Await all IScopeSetupCompletionAsync.OnSetupCompletionAsync()
            //
            // Later, when scene loads:
            // 7. Invoke all IScopeLoaded.OnLoaded()
            // 8. Await all IScopeLoadedAsync.OnLoadedAsync()
            //
            // When scope terminates:
            // 9. Invoke all IScopeDispose.OnDispose()
            // 10. Await all IScopeDisposeAsync.OnDisposeAsync()
        }
    }
}