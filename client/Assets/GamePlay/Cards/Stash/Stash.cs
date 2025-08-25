using Common.Network;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IStash
    {
        Vector2 PickPoint { get; }
    }
    
    public class Stash : IScopeLoaded, IStash
    {
        public Stash(IStashView view, NetworkProperty<PlayerStashState> state)
        {
            _view = view;
            _state = state;
        }

        private readonly NetworkProperty<PlayerStashState> _state;
        private readonly IStashView _view;

        public Vector2 PickPoint => _view.PickPoint;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, () =>
            {
                _view.UpdateAmount(_state.Value.Count);
            });
        }
    }
}