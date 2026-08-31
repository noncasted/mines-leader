using GamePlay.Loop;
using Internal;
using UnityEngine;

namespace GamePlay.Players.Resource
{
    [DisallowMultipleComponent]
    public class PlayerOwnResources : MonoBehaviour, ISceneService, ILocalPlayerCreated
    {
        [SerializeField] private PlayerResourceRow _manaRow;
        [SerializeField] private PlayerResourceRow _turnsRow;
        [SerializeField] private PlayerResourceRow _healthRow;

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

            _manaRow.Setup(lifetime, player.Mana, manaOptions, reverse: false);
            _turnsRow.Setup(lifetime, player.Turns, turnsOptions, reverse: false);
            _healthRow.Setup(lifetime, player.Health, healthOptions, reverse: false);
        }
    }
}