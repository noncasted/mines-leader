using System;
using GamePlay.Loop;
using Internal;
using UnityEngine;

namespace GamePlay.Players.Resource
{
    [DisallowMultipleComponent]
    public class PlayerOpponentResources : MonoBehaviour, ISceneService, IRemotePlayerCreated
    {
        private OpponentUIBindings _bindings;

        [Inject]
        internal void Construct(OpponentUIBindings bindings)
        {
            _bindings = bindings;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IRemotePlayerCreated>();
        }

        public void OnRemotePlayer(IReadOnlyLifetime lifetime, IGamePlayer other)
        {
            var manaOptions = new PlayerResourceOptions(
                largeBaseFull: Sprites.GameUI.ManaMediumBaseFull,
                largeBaseEmpty: Sprites.GameUI.ManaMediumBaseEmpty,
                largeAdditionalFull: Sprites.GameUI.ManaMediumAdditionalFull,
                largeAdditionalEmpty: Sprites.GameUI.ManaMediumAdditionalEmpty,
                smallBaseFull: Sprites.GameUI.ManaSmallBaseFull,
                smallBaseEmpty: Sprites.GameUI.ManaSmallBaseEmpty,
                smallAdditionalFull: Sprites.GameUI.ManaSmallAdditionalFull,
                smallAdditionalEmpty: Sprites.GameUI.ManaSmallAdditionalEmpty);

            var turnsOptions = new PlayerResourceOptions(
                largeBaseFull: Sprites.GameUI.TurnMediumBaseFull,
                largeBaseEmpty: Sprites.GameUI.TurnMediumBaseEmpty,
                largeAdditionalFull: Sprites.GameUI.TurnMediumAdditionalFull,
                largeAdditionalEmpty: Sprites.GameUI.TurnMediumAdditionalEmpty,
                smallBaseFull: Sprites.GameUI.TurnSmallBaseFull,
                smallBaseEmpty: Sprites.GameUI.TurnSmallBaseEmpty,
                smallAdditionalFull: Sprites.GameUI.TurnSmallAdditionalFull,
                smallAdditionalEmpty: Sprites.GameUI.TurnSmallAdditionalEmpty);

            var healthOptions = new PlayerResourceOptions(
                largeBaseFull: Sprites.GameUI.HealthMediumBaseFull,
                largeBaseEmpty: Sprites.GameUI.HealthMediumBaseEmpty,
                largeAdditionalFull: Sprites.GameUI.HealthMediumAdditionalFull,
                largeAdditionalEmpty: Sprites.GameUI.HealthMediumAdditionalEmpty,
                smallBaseFull: Sprites.GameUI.HealthSmallBaseFull,
                smallBaseEmpty: Sprites.GameUI.HealthSmallBaseEmpty,
                smallAdditionalFull: Sprites.GameUI.HealthSmallAdditionalFull,
                smallAdditionalEmpty: Sprites.GameUI.HealthSmallAdditionalEmpty);

            var container = _bindings.Resources.Container;

            Setup(lifetime, container.Mana, other.Mana, manaOptions);
            Setup(lifetime, container.Turns, other.Turns, turnsOptions);
            Setup(lifetime, container.Health, other.Health, healthOptions);
        }

        private static void Setup(
            IReadOnlyLifetime lifetime,
            GameOpponentResourceRowBindings row,
            IPlayerResource resource,
            PlayerResourceOptions options)
        {
            var elements = new PlayerResourceRow.Elements(
                row.LargeRow.GameObject,
                Large(row),
                row.SmallRow.GameObject,
                Small(row));

            row.PlayerResourceRow.Setup(lifetime, resource, options, elements, reverse: true);
        }

        private static PlayerResourceEntry[] Large(GameOpponentResourceRowBindings row)
        {
            var source = row.LargeRow.GameOwnResourceRowEntries;
            var entries = new PlayerResourceEntry[source.Length];

            for (var i = 0; i < source.Length; i++)
                entries[i] = source[i].PlayerResourceEntry;

            return entries;
        }

        // У чужого ряда верх и низ лежат отдельными объектами, а ряд ждёт один список в порядке
        // отрисовки, поэтому сшиваем их парами. Первый пипс верха назван иначе и в биндингах
        // лежит отдельным полем — в списке он всё равно идёт первым, как и в иерархии.
        private static PlayerResourceEntry[] Small(GameOpponentResourceRowBindings row)
        {
            var top = row.SmallRow.Top;
            var bottom = row.SmallRow.Bottom.GameOwnResourceRowEntries;
            var topCount = top.GameOwnResourceRowEntries.Length + 1;
            var pairs = Math.Min(topCount, bottom.Length);
            var entries = new PlayerResourceEntry[pairs * 2];

            for (var i = 0; i < pairs; i++)
            {
                entries[i * 2] = i == 0
                    ? top.GameOwnResourceRowEntrySmall.PlayerResourceEntry
                    : top.GameOwnResourceRowEntries[i - 1].PlayerResourceEntry;

                entries[i * 2 + 1] = bottom[i].PlayerResourceEntry;
            }

            return entries;
        }
    }
}
