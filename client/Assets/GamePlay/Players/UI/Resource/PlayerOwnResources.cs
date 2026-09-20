using GamePlay.Loop;
using Internal;
using UnityEngine;

namespace GamePlay.Players.Resource
{
    [DisallowMultipleComponent]
    public class PlayerOwnResources : MonoBehaviour, ISceneService, ILocalPlayerCreated
    {
        private PlayerUIBindings _bindings;

        [Inject]
        internal void Construct(PlayerUIBindings bindings)
        {
            _bindings = bindings;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<ILocalPlayerCreated>();
        }
        
        public void OnLocalPlayer(IReadOnlyLifetime lifetime, IGamePlayer player)
        {
            var manaOptions = new PlayerResourceOptions(
                largeBaseFull: Sprites.GameUI.ManaLargeBaseFull,
                largeBaseEmpty: Sprites.GameUI.ManaLargeBaseEmpty,
                largeAdditionalFull: Sprites.GameUI.ManaLargeAdditionalFull,
                largeAdditionalEmpty: Sprites.GameUI.ManaLargeAdditionalEmpty,
                smallBaseFull: Sprites.GameUI.ManaMediumBaseFull,
                smallBaseEmpty: Sprites.GameUI.ManaMediumBaseEmpty,
                smallAdditionalFull: Sprites.GameUI.ManaMediumAdditionalFull,
                smallAdditionalEmpty: Sprites.GameUI.ManaMediumAdditionalEmpty);

            var turnsOptions = new PlayerResourceOptions(
                largeBaseFull: Sprites.GameUI.TurnLargeBaseFull,
                largeBaseEmpty: Sprites.GameUI.TurnLargeBaseEmpty,
                largeAdditionalFull: Sprites.GameUI.TurnLargeAdditionalFull,
                largeAdditionalEmpty: Sprites.GameUI.TurnLargeAdditionalEmpty,
                smallBaseFull: Sprites.GameUI.TurnMediumBaseFull,
                smallBaseEmpty: Sprites.GameUI.TurnMediumBaseEmpty,
                smallAdditionalFull: Sprites.GameUI.TurnMediumAdditionalFull,
                smallAdditionalEmpty: Sprites.GameUI.TurnMediumAdditionalEmpty);

            var healthOptions = new PlayerResourceOptions(
                largeBaseFull: Sprites.GameUI.HealthLargeBaseFull,
                largeBaseEmpty: Sprites.GameUI.HealthLargeBaseEmpty,
                largeAdditionalFull: Sprites.GameUI.HealthLargeAdditionalFull,
                largeAdditionalEmpty: Sprites.GameUI.HealthLargeAdditionalEmpty,
                smallBaseFull: Sprites.GameUI.HealthMediumBaseFull,
                smallBaseEmpty: Sprites.GameUI.HealthMediumBaseEmpty,
                smallAdditionalFull: Sprites.GameUI.HealthMediumAdditionalFull,
                smallAdditionalEmpty: Sprites.GameUI.HealthMediumAdditionalEmpty);

            var container = _bindings.Resources.Container;

            Setup(lifetime, container.Mana, player.Mana, manaOptions);
            Setup(lifetime, container.Turns, player.Turns, turnsOptions);
            Setup(lifetime, container.Health, player.Health, healthOptions);
        }

        private static void Setup(
            IReadOnlyLifetime lifetime,
            GameOwnResourceRowBindings row,
            IPlayerResource resource,
            PlayerResourceOptions options)
        {
            var elements = new PlayerResourceRow.Elements(
                row.LargeRow.GameObject,
                Large(row),
                row.SmallRow.GameObject,
                Small(row));

            row.PlayerResourceRow.Setup(lifetime, resource, options, elements, reverse: false);
        }

        private static PlayerResourceEntry[] Large(GameOwnResourceRowBindings row)
        {
            var source = row.LargeRow.GameOwnResourceRowEntries;
            var entries = new PlayerResourceEntry[source.Length];

            for (var i = 0; i < source.Length; i++)
                entries[i] = source[i].PlayerResourceEntry;

            return entries;
        }

        // Мелкий ряд лежит в гриде на две строки с горизонтальным ходом, поэтому порядок детей
        // уже идёт парами «верхний, нижний» — ровно в том виде, в каком его ждёт ряд.
        private static PlayerResourceEntry[] Small(GameOwnResourceRowBindings row)
        {
            var source = row.SmallRow.GameOwnResourceRowEntries;
            var entries = new PlayerResourceEntry[source.Length];

            for (var i = 0; i < source.Length; i++)
                entries[i] = source[i].PlayerResourceEntry;

            return entries;
        }
    }
}
