/// <summary>
/// AI REFERENCE: Reactive Data Structures
///
/// Event-driven reactive system with automatic subscription cleanup.
/// Every subscription is scoped to a Lifetime - automatic unsubscribe on termination.
/// Used for: UI binding, state changes, event handling, async coordination.
///
/// Read: .claude/docs/COMMON_REACTIVE_BASICS.md (EventSource)
/// Also: .claude/docs/COMMON_REACTIVE_VALUES.md (ViewableProperty)
/// Also: .claude/docs/COMMON_REACTIVE_COLLECTIONS.md (ViewableList)
/// </summary>

using System.Threading.Tasks;
using Internal;

namespace Docs.Claude
{
    public class Docs_Reactive
    {
        // ==================== CORE EXAMPLES ====================

        // Example 1: EventSource - fire-and-forget events
        // Subscribers notified immediately when Invoke() called
        public void Example_EventSourceBasic()
        {
            var lifetime = new Lifetime();
            var onDamage = new EventSource<int>();

            var damageReceived = 0;
            onDamage.Advise(lifetime, damage => damageReceived = damage);

            onDamage.Invoke(50); // damageReceived == 50
            onDamage.Invoke(30); // damageReceived == 30

            lifetime.Terminate(); // Unsubscribed automatically
            onDamage.Invoke(20); // damageReceived still 30 (not called)
        }

        // Example 2: ViewableDelegate - typed event wrapper
        // Same as EventSource, but with readonly interface for public exposure
        public void Example_ViewableDelegate()
        {
            var lifetime = new Lifetime();
            var player = new MockPlayer();

            var attackCount = 0;
            player.OnAttack.Advise(lifetime, () => attackCount++);

            player.Attack(); // attackCount == 1
            player.Attack(); // attackCount == 2

            lifetime.Terminate();
            player.Attack(); // attackCount still 2
        }

        // Example 3: LifetimedValue - value with own lifetime
        // Each value has its own lifetime that terminates on Set()
        public void Example_LifetimedValue()
        {
            var lifetime = new Lifetime();
            var currentWeapon = new LifetimedValue<string>("sword");

            var changedCount = 0;
            currentWeapon.Advise(lifetime, (weaponLifetime, weapon) =>
                {
                    changedCount++;
                    // weaponLifetime valid until next Set()
                }
            );

            currentWeapon.Set("bow"); // changedCount == 1, old lifetime terminated, new created
            currentWeapon.Set("staff"); // changedCount == 2

            lifetime.Terminate();
            currentWeapon.Set("axe"); // changedCount still 2
        }

        // Example 4: ViewableProperty - convenience wrapper
        // Advise() only future, View() includes immediate callback
        public void Example_ViewableProperty()
        {
            var lifetime = new Lifetime();
            var health = new ViewableProperty<int>(100);

            var updateCount = 0;

            // Advise - only future changes
            health.Advise(lifetime, (_, value) => updateCount++);

            health.Set(90); // updateCount == 1
            health.Set(80); // updateCount == 2

            // View - immediate + future
            updateCount = 0;
            health.View(lifetime, value =>
                {
                    // Called immediately with current value (80)
                    updateCount++;
                }
            );
            // updateCount == 1 after View() called

            health.Set(70); // updateCount == 2
        }

        // Example 5: ViewableList - observable collection
        // Each item has its own lifetime that terminates on Remove()
        public void Example_ViewableList()
        {
            var lifetime = new Lifetime();
            var enemies = new ViewableList<MockEnemy>();

            var setupCount = 0;
            var cleanupCount = 0;

            // View: notified on future additions + iterate existing
            enemies.View(lifetime, (enemyLifetime, enemy) =>
                {
                    setupCount++;

                    // Item lifetime valid until enemy removed
                    enemyLifetime.Listen(() => cleanupCount++);
                }
            );

            var enemy1 = new MockEnemy();
            enemies.Add(enemy1); // setupCount == 1

            var enemy2 = new MockEnemy();
            enemies.Add(enemy2); // setupCount == 2

            enemies.Remove(enemy1); // cleanupCount == 1
            enemies.Remove(enemy2); // cleanupCount == 2

            lifetime.Terminate();
        }

        // Example 6: ViewableDictionary - observable key-value map
        // Same as ViewableList but with keys
        public void Example_ViewableDictionary()
        {
            var lifetime = new Lifetime();
            var sessions = new ViewableDictionary<string, MockSession>();

            var setupCount = 0;

            sessions.View(lifetime, (sessionLifetime, key, session) =>
                {
                    setupCount++;
                    // sessionLifetime valid until key removed
                }
            );

            sessions.Add("user1", new MockSession()); // setupCount == 1
            sessions.Add("user2", new MockSession()); // setupCount == 2

            sessions.Remove("user1"); // sessionLifetime for user1 terminated

            lifetime.Terminate();
        }

        // ==================== CORE PATTERNS ====================

        // Pattern: Advise vs View
        // Advise = future events only
        // View = immediate callback + future events
        public void Pattern_AdviseVsView()
        {
            var lifetime = new Lifetime();
            var health = new ViewableProperty<int>(100);

            var adviseCallCount = 0;
            health.Advise(lifetime, (_, value) =>
                {
                    adviseCallCount++; // Called only on Set()
                }
            );
            // adviseCallCount still 0 - no immediate callback

            var viewCallCount = 0;
            health.View(lifetime, value =>
                {
                    viewCallCount++; // Called immediately + on Set()
                }
            );
            // viewCallCount == 1 - immediate callback with current value (100)

            health.Set(90);
            // adviseCallCount == 1
            // viewCallCount == 2
        }

        // Pattern: UI Binding
        // View property, update UI immediately and on every change
        public void Pattern_UIBinding()
        {
            var lifetime = new Lifetime();
            var healthProperty = new ViewableProperty<int>(100);

            var uiUpdateCount = 0;
            healthProperty.View(lifetime, value =>
                {
                    uiUpdateCount++; // UI updated with current and future values
                    // In real code: healthBar.SetValue(value)
                }
            );

            healthProperty.Set(90); // uiUpdateCount == 2 (immediate + 1 update)
            healthProperty.Set(50); // uiUpdateCount == 3
        }

        // Pattern: Item-scoped Subscription
        // Subscribe to item events with item lifetime - auto cleanup on removal
        public void Pattern_ItemScoped()
        {
            var lifetime = new Lifetime();
            var enemies = new ViewableList<MockEnemy>();

            var deathCount = 0;

            enemies.View(lifetime, (enemyLifetime, enemy) =>
                {
                    // Subscribe to enemy death with enemy lifetime
                    enemy.OnDeath.Advise(enemyLifetime, () => deathCount++);

                    // When enemy removed from list, this subscription auto-cleaned
                }
            );

            var enemy1 = new MockEnemy();
            enemies.Add(enemy1);

            enemy1.Die(); // deathCount == 1

            enemies.Remove(enemy1);
            // enemyLifetime terminated, subscription removed

            enemy1.Die(); // deathCount still 1 (subscription already removed)
        }

        // Pattern: Conditional Logic
        // React to state changes with business logic
        public void Pattern_ConditionalLogic()
        {
            var lifetime = new Lifetime();
            var isConnected = new ViewableProperty<bool>(false);

            var onlineCount = 0;
            var offlineCount = 0;

            isConnected.View(lifetime, connected =>
                {
                    if (connected)
                    {
                        onlineCount++;
                        // Show online UI
                    }
                    else
                    {
                        offlineCount++;
                        // Show offline UI
                    }
                }
            );

            isConnected.Set(true); // onlineCount == 1, offlineCount == 1 (immediate)
            isConnected.Set(false); // onlineCount == 1, offlineCount == 2
            isConnected.Set(true); // onlineCount == 2, offlineCount == 2
        }

        // Pattern: Async Coordination
        // Wait for conditions and events with async/await
        public async Task Pattern_AsyncCoordination()
        {
            var lifetime = new Lifetime();
            var isReady = new ViewableProperty<bool>(false);
            var onComplete = new EventSource();

            // Wait for ready state
            var readyTask = isReady.WaitTrue(lifetime);
            isReady.Set(true);
            await readyTask;

            // Wait for completion event
            var completeTask = onComplete.WaitInvoke(lifetime);
            onComplete.Invoke();
            await completeTask;

            lifetime.Terminate();
        }

        // Pattern: Async Waiting Examples
        public async Task Pattern_AsyncWaiting()
        {
            var lifetime = new Lifetime();

            // Wait for event to fire
            var onEvent = new EventSource<int>();
            var valueTask = onEvent.WaitInvoke<int>(lifetime);
            onEvent.Invoke(42);
            var value = await valueTask; // value == 42

            // Wait for bool property condition
            var isReady = new ViewableProperty<bool>(false);
            var readyTask = isReady.WaitTrue(lifetime);
            isReady.Set(true);
            await readyTask;

            // Wait for false condition
            var isActive = new ViewableProperty<bool>(true);
            var inactiveTask = isActive.WaitFalse(lifetime);
            isActive.Set(false);
            await inactiveTask;

            lifetime.Terminate();
        }

        // ==================== SPECIAL CASES ====================

        // Special: ModifiableList - iteration-safe list
        // Can Add/Remove during iteration without ConcurrentModificationException
        public void Special_ModifiableList()
        {
            var list = new ModifiableList<int>();

            list.Add(1);
            list.Add(2);
            list.Add(3);

            var sum = 0;
            foreach (var item in list)
            {
                sum += item;

                // Safe to modify during iteration
                if (item == 2)
                {
                    list.Add(4); // Added, but not in current iteration
                }
            }

            // sum == 6 (1+2+3), iteration complete
            // list now contains [1,2,3,4]
        }

        // ==================== RULES ====================

        // RULE 1: All Advise/View require Lifetime
        // No naked subscriptions = no memory leaks
        public void Rule1_LifetimeRequired()
        {
            var lifetime = new Lifetime();
            var onEvent = new EventSource();

            var called = false;

            // CORRECT: with lifetime
            onEvent.Advise(lifetime, () => called = true);

            lifetime.Terminate();
            // Unsubscribed automatically - no memory leak

            // WRONG (commented out): without lifetime - memory leak!
            // onEvent.Advise(action => called = true); // Keeps subscription forever
        }

        // RULE 2: View = Advise + immediate
        // View calls handler immediately, Advise only on future changes
        public void Rule2_ViewAdviseImmediate()
        {
            var lifetime = new Lifetime();
            var property = new ViewableProperty<int>(100);

            var adviseCount = 0;
            var viewCount = 0;

            property.Advise(lifetime, (_, value) => adviseCount++);
            // adviseCount == 0 - no immediate call

            property.View(lifetime, value => viewCount++);
            // viewCount == 1 - immediate call

            property.Set(90);
            // adviseCount == 1
            // viewCount == 2
        }

        // RULE 3: Item lifetime = item existence
        // Remove item = terminate its lifetime
        public void Rule3_ItemLifetime()
        {
            var lifetime = new Lifetime();
            var items = new ViewableList<string>();

            var lifetimeCleanedCount = 0;

            items.View(lifetime, (itemLifetime, item) =>
                {
                    itemLifetime.Listen(() => lifetimeCleanedCount++);
                }
            );

            items.Add("item1"); // lifetimeCleanedCount == 0
            items.Add("item2"); // lifetimeCleanedCount == 0

            items.Remove("item1"); // lifetimeCleanedCount == 1
            items.Remove("item2"); // lifetimeCleanedCount == 2

            // When main lifetime terminates, all remaining items' lifetimes terminate
            lifetime.Terminate();
        }

        // RULE 4: Value lifetime = value validity
        // Set() = terminate old lifetime, create new
        public void Rule4_ValueLifetime()
        {
            var lifetime = new Lifetime();
            var currentValue = new LifetimedValue<string>("initial");

            var valueLifetimeTerminatedCount = 0;

            currentValue.Advise(lifetime, (valueLifetime, value) =>
                {
                    valueLifetime.Listen(() => valueLifetimeTerminatedCount++);
                    // Each value has its own lifetime
                }
            );

            currentValue.Set("second"); // Previous value lifetime terminated
            // valueLifetimeTerminatedCount == 1

            currentValue.Set("third"); // Second value lifetime terminated
            // valueLifetimeTerminatedCount == 2

            lifetime.Terminate();
            // Third value lifetime also terminates
            // valueLifetimeTerminatedCount == 3
        }

        // RULE 5: Dispose clears all
        // Dispose() clears all subscriptions and lifetimes
        public void Rule5_DisposeClears()
        {
            var eventSource = new EventSource();
            var lifetime = new Lifetime();

            var callCount = 0;
            eventSource.Advise(lifetime, () => callCount++);

            eventSource.Invoke(); // callCount == 1

            eventSource.Dispose(); // Clears all subscriptions

            eventSource.Invoke(); // callCount still 1 - no subscribers

            lifetime.Terminate(); // Safe to terminate after dispose
        }

        // ==================== HELPER CLASSES ====================

        private class MockPlayer
        {
            private readonly ViewableDelegate _onAttack = new();

            public IViewableDelegate OnAttack => _onAttack;

            public void Attack()
            {
                _onAttack.Invoke();
            }
        }

        private class MockEnemy
        {
            private readonly ViewableDelegate _onDeath = new();

            public IViewableDelegate OnDeath => _onDeath;

            public void Die()
            {
                _onDeath.Invoke();
            }
        }

        private class MockSession
        {
        }
    }
}