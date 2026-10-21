using System.Collections.Generic;
using NUnit.Framework;

namespace Internal.Tests
{
    public class LifetimedValueTests
    {
        [Test]
        public void DefaultOfValueType_HasValueLifetime()
        {
            var flag = new ViewableProperty<bool>(false);
            var number = new ViewableProperty<int>();

            Assert.IsNotNull(flag.ValueLifetime);
            Assert.IsNotNull(number.ValueLifetime);
            Assert.IsFalse(flag.ValueLifetime.IsTerminated);
        }

        [Test]
        public void View_DefaultOfValueType_GetsLiveLifetime()
        {
            var lifetime = new Lifetime();
            var property = new ViewableProperty<bool>(false);
            IReadOnlyLifetime viewed = null;

            property.View(lifetime, (valueLifetime, _) => viewed ??= valueLifetime);

            Assert.IsNotNull(viewed);
            Assert.IsFalse(viewed.IsTerminated);

            property.Set(true);

            Assert.IsTrue(viewed.IsTerminated);
        }

        [Test]
        public void Set_Equal_DoesNotNotifyOrTerminate()
        {
            var lifetime = new Lifetime();
            var property = new ViewableProperty<int>(0);
            var initial = property.ValueLifetime;
            var calls = 0;

            property.Advise(lifetime, (_, _) => calls++);
            property.Set(0);

            Assert.AreEqual(0, calls);
            Assert.IsFalse(initial.IsTerminated);
        }

        [Test]
        public void Set_Changed_TerminatesPreviousValueLifetime()
        {
            var property = new ViewableProperty<int>(1);
            var initial = property.ValueLifetime;

            property.Set(2);

            Assert.IsTrue(initial.IsTerminated);
            Assert.IsFalse(property.ValueLifetime.IsTerminated);
        }

        [Test]
        public void AddClampMax_ListenersNeverSeeValueOverMax()
        {
            var lifetime = new Lifetime();
            var property = new ViewableProperty<int>(8);
            var seen = new List<int>();

            property.Advise(lifetime, (_, value) => seen.Add(value));

            Assert.AreEqual(10, property.AddClampMax(5, 10));
            Assert.AreEqual(10, property.IncreaseClampMax(10));
            Assert.AreEqual(10, property.Value);

            CollectionAssert.AreEqual(new[] { 10 }, seen);
        }

        [Test]
        public void IncreaseClampMax_BelowMax_Increases()
        {
            var property = new ViewableProperty<int>(3);

            Assert.AreEqual(4, property.IncreaseClampMax(10));
            Assert.AreEqual(4, property.Value);
        }
    }
}
