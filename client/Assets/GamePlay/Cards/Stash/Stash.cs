using UnityEngine;

namespace GamePlay.Cards
{
    public interface IStash
    {
        Vector2 PickPoint { get; }

        void SetCount(int count);
    }

    public class Stash : IStash
    {
        public Stash(IStashView view)
        {
            _view = view;
        }

        private readonly IStashView _view;

        public Vector2 PickPoint => _view.PickPoint;

        public void SetCount(int count)
        {
            _view.UpdateAmount(count);
        }
    }
}
