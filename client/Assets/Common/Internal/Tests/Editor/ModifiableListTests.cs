using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Internal.Tests
{
    public class ModifiableListTests
    {
        [Test]
        public void Add_OutsideIteration_IsVisibleImmediately()
        {
            var list = new ModifiableList<int>();

            list.Add(1);
            list.Add(2);

            CollectionAssert.AreEqual(new[] { 1, 2 }, Collect(list));
        }

        [Test]
        public void Add_DuringIteration_AppliesAfterPass()
        {
            var list = new ModifiableList<int>();
            list.Add(1);

            var seen = new List<int>();

            foreach (var item in list)
            {
                seen.Add(item);
                list.Add(item + 10);
            }

            CollectionAssert.AreEqual(new[] { 1 }, seen);
            CollectionAssert.AreEqual(new[] { 1, 11 }, Collect(list));
        }

        [Test]
        public void Remove_DuringIteration_StillVisitedThenRemoved()
        {
            var list = new ModifiableList<int>();
            list.Add(1);
            list.Add(2);
            list.Add(3);

            var seen = new List<int>();

            foreach (var item in list)
            {
                seen.Add(item);
                list.Remove(2);
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, seen);
            CollectionAssert.AreEqual(new[] { 1, 3 }, Collect(list));
        }

        [Test]
        public void AddThenRemove_DuringIteration_NeverLands()
        {
            var list = new ModifiableList<int>();
            list.Add(1);

            foreach (var _ in list)
            {
                list.Add(5);
                list.Remove(5);
            }

            CollectionAssert.AreEqual(new[] { 1 }, Collect(list));
        }

        [Test]
        public void Break_DuringIteration_DoesNotLeaveListIterating()
        {
            var list = new ModifiableList<int>();
            list.Add(1);
            list.Add(2);

            foreach (var _ in list)
                break;

            list.Add(3);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Collect(list));
        }

        [Test]
        public void Exception_DuringIteration_DoesNotLeaveListIterating()
        {
            var list = new ModifiableList<int>();
            list.Add(1);

            Assert.Throws<InvalidOperationException>(() => {
                foreach (var _ in list)
                {
                    list.Add(2);
                    throw new InvalidOperationException();
                }
            });

            list.Add(3);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Collect(list));
        }

        [Test]
        public void NestedIteration_AppliesPendingAfterOutermostPass()
        {
            var list = new ModifiableList<int>();
            list.Add(1);
            list.Add(2);

            var innerPasses = 0;

            foreach (var _ in list)
            {
                foreach (var __ in list)
                    innerPasses++;

                list.Add(3);

                // Внутренний проход завершился, но внешний ещё идёт: элемент отложен.
                Assert.AreEqual(2, list.Count);
            }

            Assert.AreEqual(4, innerPasses);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 3 }, Collect(list));
        }

        [Test]
        public void Clear_DuringIteration_AppliesAfterPassAndDropsPending()
        {
            var list = new ModifiableList<int>();
            list.Add(1);
            list.Add(2);

            var seen = new List<int>();

            foreach (var item in list)
            {
                seen.Add(item);
                list.Add(9);
                list.Clear();
            }

            CollectionAssert.AreEqual(new[] { 1, 2 }, seen);
            CollectionAssert.IsEmpty(Collect(list));
        }

        [Test]
        public void Clear_OutsideIteration_Empties()
        {
            var list = new ModifiableList<int>();
            list.Add(1);

            list.Clear();

            Assert.AreEqual(0, list.Count);
            CollectionAssert.IsEmpty(Collect(list));
        }

        private static List<T> Collect<T>(ModifiableList<T> list)
        {
            var result = new List<T>();

            foreach (var item in list)
                result.Add(item);

            return result;
        }
    }
}
