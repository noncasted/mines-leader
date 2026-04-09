/// <summary>
/// AI REFERENCE: Lifetime Management System
///
/// Resource lifecycle management system with automatic cleanup on termination.
/// Used everywhere for: subscriptions, async operations, service management, cleanup logic.
///
/// Read: .claude/docs/COMMON_LIFETIMES.md
/// </summary>

using System.Collections.Generic;
using Internal;

namespace Docs.Claude
{
    public class Docs_Lifetimes
    {
        public void Example_Standalone()
        {
            var lifetime = new Lifetime();
            lifetime.Listen(Execute);

            lifetime.Terminate(); // Invokes Execute() method
            return;

            void Execute()
            {

            }
        }

        public void Example_Child()
        {
            var lifetime = new Lifetime();

            var childLifetime = lifetime.Child();
            childLifetime.Listen(Execute);

            lifetime.Terminate(); // Terminates self and child and invokes Execute() method
            return;

            void Execute()
            {

            }
        }

        // ==================== CREATING LIFETIMES ====================

        // Recipe 3: Hierarchy lifetimes for complex flow
        // Demonstrates hierarchy: when parent terminates, children terminate too
        public void Creating_LifetimeHierarchy()
        {
            var gameLifetime = new Lifetime();
            var levelLifetime = gameLifetime.Child();
            var bossLifetime = levelLifetime.Child();

            bossLifetime.Listen(OnBossCleanup);
            levelLifetime.Listen(OnLevelCleanup);
            gameLifetime.Listen(OnGameCleanup);

            gameLifetime.Terminate(); // Terminates all: boss, level, game
            return;

            void OnBossCleanup()
            {
                // Boss saved
            }

            void OnLevelCleanup()
            {
                // Level unloaded
            }

            void OnGameCleanup()
            {
                // Game finished
            }
        }

        // Recipe 4: Intersection - terminate when FIRST of two lifetimes ends
        // Useful for timeouts and race conditions
        public void Creating_IntersectLifetime()
        {
            var operationLifetime = new Lifetime();
            var timeoutLifetime = new Lifetime();

            var intersectedLifetime = operationLifetime.Intersect(timeoutLifetime);
            intersectedLifetime.Listen(OnOperationComplete);

            timeoutLifetime.Terminate(); // Example: timeout occurred first
            return;

            void OnOperationComplete()
            {
                // Operation finished (timeout or success)
            }
        }

        // ==================== CORE PATTERNS ====================

        // Pattern 1: Scoped Subscription
        // Automatically unsubscribe when lifetime ends
        // Used everywhere for event subscriptions
        public void Pattern_ScopedSubscription()
        {
            var lifetime = new Lifetime();

            // Demonstrate subscription (in reality this is EventSource.Advise)
            var subscriptionCount = 0;
            lifetime.Listen(OnUnsubscribe);

            lifetime.Terminate(); // Subscription is automatically removed
            return;

            void OnUnsubscribe()
            {
                subscriptionCount--;
            }
        }

        // Pattern 2: Resource Cleanup
        // Listen() guarantees cleanup logic will execute
        public void Pattern_ResourceCleanup()
        {
            var lifetime = new Lifetime();

            var connection = new SimpleConnection();
            lifetime.Listen(() => connection.Close());

            lifetime.Terminate(); // Connection.Close() called automatically
            return;
        }

        // Pattern 3: Conditional Lifetime (enable/disable subsystem)
        // Create/destroy services dynamically
        public void Pattern_ConditionalLifetime()
        {
            var gameLifetime = new Lifetime();
            ILifetime achievementLifetime = null;

            EnableAchievements(gameLifetime, true);
            // achievementSystem initialized

            EnableAchievements(gameLifetime, false);
            // achievementSystem cleaned

            gameLifetime.Terminate();
            return;

            void EnableAchievements(IReadOnlyLifetime gameLifetime, bool enable)
            {
                if (enable && achievementLifetime == null)
                {
                    achievementLifetime = gameLifetime.Child();
                    // _achievementSystem.Initialize(achievementLifetime);
                }
                else if (!enable && achievementLifetime != null)
                {
                    achievementLifetime.Terminate();
                    achievementLifetime = null;
                }
            }
        }

        // Pattern 4: Check IsTerminated in loops
        // Proper async operation termination
        public void Pattern_CheckIsTerminated()
        {
            var lifetime = new Lifetime();
            var itemsProcessed = 0;

            ProcessQueue(lifetime);

            lifetime.Terminate();
            return;

            void ProcessQueue(IReadOnlyLifetime lt)
            {
                while (lt.IsTerminated == false)
                {
                    itemsProcessed++;

                    // Simulate work
                    if (itemsProcessed >= 3)
                        break;
                }
            }
        }


        // ==================== SPECIAL CASES ====================

        // TerminatedLifetime (already terminated)
        // Used when operation is cancelled BEFORE lifetime creation
        // Important: Listen() on TerminatedLifetime will NOT call callback!
        public void Special_TerminatedLifetime()
        {
            IReadOnlyLifetime dialogueLifetime;

            if (HasDialogue("invalid"))
            {
                dialogueLifetime = new Lifetime();
            }
            else
            {
                dialogueLifetime = new TerminatedLifetime(); // Operation cancelled
            }

            // Listen on TerminatedLifetime does nothing!
            var callbackExecuted = false;
            dialogueLifetime.Listen(() => callbackExecuted = true);

            return;

            bool HasDialogue(string id) => false;
        }

        // CancellationToken is created lazily
        // Token is only created when first accessed
        public void Special_LazyToken()
        {
            var lifetime = new Lifetime();

            // CancellationTokenSource not created yet
            var isTerminated = lifetime.IsTerminated; // This does not create TokenSource

            var token = lifetime.Token; // CancellationTokenSource created here
            lifetime.Terminate();
            return;
        }


        // ==================== RULES ====================

        // RULE 1: Listen on terminated lifetime = callback fires IMMEDIATELY
        // If lifetime is already terminated, callback executes synchronously
        public void Rule1_ImmediateCallback()
        {
            var lt = new Lifetime();
            lt.Terminate();

            var firedImmediately = false;
            lt.Listen(() => firedImmediately = true); // Fires synchronously!

            return;
        }

        // RULE 2: Child automatically terminates when parent terminates
        // No need to call Terminate() on child if parent is already terminated
        public void Rule2_ChildTermination()
        {
            var parent = new Lifetime();
            var child = parent.Child();

            var childCleaned = false;
            child.Listen(() => childCleaned = true);

            parent.Terminate();
            // child is also terminated - childCleaned is already true
            return;
        }

        // RULE 3: Terminate() can be called multiple times - it's a no-op
        // Safe to call Terminate() a second time
        public void Rule3_MultipleTerminate()
        {
            var lt = new Lifetime();
            var callCount = 0;

            lt.Listen(() => callCount++);

            lt.Terminate(); // callCount == 1
            lt.Terminate(); // OK - just no-op, callCount still 1
            return;
        }

        // RULE 4: Listeners are cleared after termination
        // After terminate(), new listeners won't be stored and won't take memory
        public void Rule4_ListenerCleanup()
        {
            var lt = new Lifetime();
            var cleanupCount = 0;

            lt.Listen(() => cleanupCount++);
            lt.Listen(() => cleanupCount++);

            lt.Terminate(); // cleanupCount == 2

            // New listener executes synchronously but won't be stored
            lt.Listen(() => cleanupCount++); // cleanupCount == 3

            // If we try to add again, nothing happens
            lt.Listen(() => cleanupCount++); // cleanupCount == 3, unchanged
            return;
        }

        // RULE 5: Terminate all children when parent terminates
        // Entire hierarchy terminates recursively
        public void Rule5_HierarchyTermination()
        {
            var parent = new Lifetime();
            var child1 = parent.Child();
            var child2 = parent.Child();
            var grandchild = child1.Child();

            var terminationOrder = new List<string>();

            parent.Listen(() => terminationOrder.Add("parent"));
            child1.Listen(() => terminationOrder.Add("child1"));
            child2.Listen(() => terminationOrder.Add("child2"));
            grandchild.Listen(() => terminationOrder.Add("grandchild"));

            parent.Terminate(); // All terminate immediately
            // terminationOrder contains all 4 elements
            return;
        }


        // ==================== DEBUGGING TIPS ====================

        // TIP 1: WRONG - lifetime never terminates, UI stays in memory
        // Common mistake: forgot that lifetime must terminate somewhere
        public void Tip1_DebugMemoryLeak_WRONG()
        {
            var foreverLifetime = new Lifetime();

            var cleaned = false;
            foreverLifetime.Listen(() => cleaned = true);

            // foreverLifetime never terminates -> cleaned stays false forever
            // UI and resources stay in memory
            return;
        }

        // TIP 1: RIGHT - lifetime properly terminates
        public void Tip1_DebugMemoryLeak_RIGHT()
        {
            var sceneLifetime = new Lifetime();

            var cleaned = false;
            sceneLifetime.Listen(() => cleaned = true);

            // When scene ends, lifetime terminates and cleaned == true
            sceneLifetime.Terminate();
            return;
        }

        // TIP 2: Use child lifetime for logic isolation
        // If you need to cancel only part of operation - use child
        public void Tip2_ChildIsolation()
        {
            var sceneLifetime = new Lifetime();

            var dialoguesCleaned = 0;

            // Each dialogue can be cancelled independently
            for (int i = 0; i < 3; i++)
            {
                var dialogueLifetime = sceneLifetime.Child();
                dialogueLifetime.Listen(() => dialoguesCleaned++);

                // Can cancel individual dialogue
                if (i == 1)
                {
                    dialogueLifetime.Terminate(); // Only this dialogue cleaned
                }
            }

            sceneLifetime.Terminate(); // All dialogues cleaned
            return;
        }

        // TIP 3: WRONG - lifetime with very long lifecycle
        // Keeps many subscribers in memory even after they're no longer needed
        private ILifetime _appLifetime = new Lifetime();

        public void Tip3_LifetimeScope_WRONG()
        {
            var subscriptionCount = 0;

            // Subscribe to app lifetime
            for (int i = 0; i < 100; i++)
            {
                _appLifetime.Listen(() => subscriptionCount++);
            }

            // All 100 listeners stay in memory until end of app!
            return;
        }

        // TIP 3: RIGHT - lifetime ends when needed
        public void Tip3_LifetimeScope_RIGHT()
        {
            var appLifetime = new Lifetime();
            var subscriptionCount = 0;

            ShowDialogue(appLifetime);

            appLifetime.Terminate();
            return;

            void ShowDialogue(IReadOnlyLifetime parentLifetime)
            {
                var dialogueLifetime = parentLifetime.Child();

                for (int i = 0; i < 100; i++)
                {
                    dialogueLifetime.Listen(() => subscriptionCount++);
                }

                // All listeners cleared when dialogue closes
                dialogueLifetime.Terminate();
            }
        }

        // TIP 4: Use IsTerminated for graceful shutdown
        // Check status before accessing resources
        public void Tip4_GracefulShutdown()
        {
            var lifetime = new Lifetime();
            var resourceAccessed = 0;

            ProcessResource(lifetime);

            lifetime.Terminate();
            return;

            void ProcessResource(IReadOnlyLifetime lt)
            {
                while (lt.IsTerminated == false)
                {
                    resourceAccessed++;

                    if (resourceAccessed >= 5)
                        break;
                }
            }
        }

        // ==================== HELPER CLASSES ====================

        private class SimpleConnection
        {
            public void Close()
            {
                // Close connection
            }
        }
    }
}