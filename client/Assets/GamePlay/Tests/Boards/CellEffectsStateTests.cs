using System;
using System.Collections.Generic;
using GamePlay.Boards;
using GamePlay.Boards.Effects;
using Internal;
using NUnit.Framework;
using Shared;
using UnityEngine;

namespace GamePlay.Tests
{
    // The effects state of a cell: entries by the id of the play, their lifetimes as a view sees them, the clear on a
    // change of the cell status.
    public class CellEffectsStateTests
    {
        private Lifetime _lifetime;
        private FakeCell _cell;
        private Dictionary<Guid, IReadOnlyLifetime> _viewed;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
            _cell = new FakeBoard(new Vector2Int(3, 3), 1f, Vector2.zero).Cell(1, 1);
            _viewed = new Dictionary<Guid, IReadOnlyLifetime>();

            _cell.Effects.Active.View(_lifetime, (entryLifetime, effectId, _) => _viewed.Add(effectId, entryLifetime));
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
        }

        [Test]
        public void Add_ShownWithALiveEntry()
        {
            var id = Guid.NewGuid();

            _cell.Effects.Add(id, CellEffectType.Frost);

            CollectionAssert.AreEqual(new[] { CellEffectType.Frost }, _cell.Effects.Active.Values);
            Assert.IsFalse(_viewed[id].IsTerminated);
        }

        [Test]
        public void Add_SameIdTwice_Throws()
        {
            var id = Guid.NewGuid();
            _cell.Effects.Add(id, CellEffectType.Smoke);

            Assert.Throws<InvalidOperationException>(() => _cell.Effects.Add(id, CellEffectType.Smoke));
        }

        [Test]
        public void Remove_EndsOnlyItsEntry()
        {
            var removed = Guid.NewGuid();
            var kept = Guid.NewGuid();
            _cell.Effects.Add(removed, CellEffectType.Fog);
            _cell.Effects.Add(kept, CellEffectType.Blackout);

            _cell.Effects.Remove(removed);

            CollectionAssert.AreEqual(new[] { CellEffectType.Blackout }, _cell.Effects.Active.Values);
            Assert.IsTrue(_viewed[removed].IsTerminated);
            Assert.IsFalse(_viewed[kept].IsTerminated);
        }

        [Test]
        public void Remove_UnknownId_Ignored()
        {
            _cell.Effects.Add(Guid.NewGuid(), CellEffectType.Fog);

            _cell.Effects.Remove(Guid.NewGuid());

            Assert.AreEqual(1, _cell.Effects.Active.Count);
        }

        [Test]
        public void StatusChange_ClearsAndEndsEveryEntry()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            _cell.Effects.Add(first, CellEffectType.Smoke);
            _cell.Effects.Add(second, CellEffectType.MineHighlight);

            _cell.EnsureFree();

            Assert.AreEqual(0, _cell.Effects.Active.Count);
            Assert.IsTrue(_viewed[first].IsTerminated);
            Assert.IsTrue(_viewed[second].IsTerminated);
        }

        [Test]
        public void StatusChange_ThenAddOfTheSameSnapshot_EffectStays()
        {
            _cell.Effects.Add(Guid.NewGuid(), CellEffectType.Smoke);
            _cell.EnsureFree();
            var added = Guid.NewGuid();

            _cell.Effects.Add(added, CellEffectType.Frost);
            _cell.EnsureFree();

            CollectionAssert.AreEqual(new[] { CellEffectType.Frost }, _cell.Effects.Active.Values);
            Assert.IsFalse(_viewed[added].IsTerminated);
        }

        [Test]
        public void StatusChange_ThenRemovalSentByTheServer_Ignored()
        {
            var id = Guid.NewGuid();
            _cell.Effects.Add(id, CellEffectType.Blackout);
            _cell.EnsureFree();

            _cell.Effects.Remove(id);

            Assert.AreEqual(0, _cell.Effects.Active.Count);
        }

        [Test]
        public void SameStatus_KeepsTheEffects()
        {
            var id = Guid.NewGuid();
            _cell.Effects.Add(id, CellEffectType.Frost);

            _cell.EnsureTaken();

            Assert.AreEqual(1, _cell.Effects.Active.Count);
            Assert.IsFalse(_viewed[id].IsTerminated);
        }

        [Test]
        public void EndOfTheViewLifetime_EndsTheViewedEntries()
        {
            var id = Guid.NewGuid();
            _cell.Effects.Add(id, CellEffectType.Fog);

            _lifetime.Terminate();

            Assert.IsTrue(_viewed[id].IsTerminated);
            Assert.AreEqual(1, _cell.Effects.Active.Count);
        }
    }
}
