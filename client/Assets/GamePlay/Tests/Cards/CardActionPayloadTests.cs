using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Cards;
using Internal;
using NUnit.Framework;
using Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace GamePlay.Tests
{
    // A card action builds the whole payload of its use: the card type comes from the card scope (a Max variant
    // shares the action of the normal card), nobody sets it on the result later.
    public class CardActionPayloadTests
    {
        private Lifetime _lifetime;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
        }

        [UnityTest]
        public IEnumerator SelfCast_Dropped_PayloadOfTheCardType() => UniTask.ToCoroutine(async () => {
            var action = new CardMedicAction(new FakeDropDetector(true), CardType.Medic);

            var result = await action.TryUse(_lifetime);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Payload, Is.TypeOf<CardUsePayload.Medic>());
            Assert.That(result.Payload.Type, Is.EqualTo(CardType.Medic));
        });

        [UnityTest]
        public IEnumerator SelfCast_NotDropped_NotSuccess() => UniTask.ToCoroutine(async () => {
            var action = new CardMedicAction(new FakeDropDetector(false), CardType.Medic);

            var result = await action.TryUse(_lifetime);

            Assert.That(result.IsSuccess, Is.False);
        });

        [UnityTest]
        public IEnumerator Board_Dropped_PayloadOfTheCardTypeAndPosition() => UniTask.ToCoroutine(async () => {
            var dropArea = new FakeDropArea(new CardDropResult(true, new Vector2Int(2, 3)));
            var action = new CardFrostAction(
                new FakeCardContext(CardType.Frost_Max),
                dropArea,
                new FakePointerHandler(),
                new CardConfigOptions.Frost());

            var result = await action.TryUse(_lifetime);

            Assert.That(result.IsSuccess, Is.True);
            var payload = (CardUsePayload.Frost)result.Payload;
            Assert.That(payload.Type, Is.EqualTo(CardType.Frost_Max));
            Assert.That(payload.Position, Is.EqualTo(new Position(2, 3)));
        });

        [UnityTest]
        public IEnumerator Board_Missed_NotSuccess() => UniTask.ToCoroutine(async () => {
            var action = new CardFrostAction(
                new FakeCardContext(CardType.Frost),
                new FakeDropArea(CardDropResult.Failed),
                new FakePointerHandler(),
                new CardConfigOptions.Frost());

            var result = await action.TryUse(_lifetime);

            Assert.That(result.IsSuccess, Is.False);
        });

        private class FakeDropDetector : ICardDropDetector
        {
            public FakeDropDetector(bool isDropped)
            {
                _isDropped = isDropped;
            }

            private readonly bool _isDropped;

            public UniTask<bool> Wait(IReadOnlyLifetime lifetime) => UniTask.FromResult(_isDropped);
        }

        private class FakeDropArea : ICardDropArea
        {
            public FakeDropArea(CardDropResult result)
            {
                _result = result;
            }

            private readonly CardDropResult _result;

            public UniTask<CardDropResult> Show(
                IReadOnlyLifetime stateLifetime,
                IReadOnlyLifetime selectionLifetime,
                ICardDropPattern pattern)
            {
                return UniTask.FromResult(_result);
            }
        }

        private class FakePointerHandler : ICardPointerHandler
        {
            public IViewableProperty<bool> IsHovered { get; } = new ViewableProperty<bool>(true);
            public IViewableProperty<bool> IsPressed { get; } = new ViewableProperty<bool>(true);
        }

        private class FakeCardContext : ICardContext
        {
            public FakeCardContext(CardType type)
            {
                Type = type;
            }

            public IBoard TargetBoard => null;
            public IViewableProperty<bool> IsAvailable => throw new NotSupportedException();
            public CardType Type { get; }
            public ICardConfig Config => throw new NotSupportedException();
        }
    }
}
