using GamePlay.Loop;
using Internal;
using Tools;
using UnityEngine;

namespace GamePlay.Players.Resource
{
    [DisallowMultipleComponent]
    public class PlayerOpponentResources : MonoBehaviour, ISceneService, IRemotePlayerCreated
    {
        [SerializeField] private PlayerResourceRow _manaRow;
        [SerializeField] private PlayerResourceRow _turnsRow;
        [SerializeField] private PlayerResourceRow _healthRow;

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
            
            _manaRow.Setup(lifetime, other.Mana, manaOptions, reverse: true);
            _turnsRow.Setup(lifetime, other.Turns, turnsOptions, reverse: true);
            _healthRow.Setup(lifetime, other.Health, healthOptions, reverse: true);
        }
    }
}