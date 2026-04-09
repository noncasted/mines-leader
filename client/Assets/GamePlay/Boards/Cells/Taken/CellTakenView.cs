using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class CellTakenView : MonoBehaviour
    {
        [SerializeField] private GameObject _mine;
        [SerializeField] private FlagAnimator _flagAnimator;

        public FlagAnimator FlagAnimator => _flagAnimator;

        public void Enable(IReadOnlyLifetime lifetime, ICellTakenState state)
        {
            gameObject.SetActive(true);
            _mine.SetActive(false);

            var previous = false;
            var animationLifetime = lifetime.Child();

            state.IsFlagged.View(lifetime, isFlagged => {
                if (isFlagged == previous)
                    return;

                previous = isFlagged;
                animationLifetime.Terminate();
                animationLifetime = lifetime.Child();
                
                if (isFlagged == true)
                    Appear(animationLifetime).NoAwait();
                else
                    Remove(animationLifetime).NoAwait();
            });

            lifetime.Listen(() => gameObject.SetActive(false));
        }

        public void OnExplosion()
        {
            gameObject.SetActive(false);
            _mine.SetActive(true);
        }

        private async UniTaskVoid Appear(IReadOnlyLifetime lifetime)
        {
            _flagAnimator.gameObject.SetActive(true);
            await _flagAnimator.PlayAppear(lifetime);
        }

        private async UniTaskVoid Remove(IReadOnlyLifetime lifetime)
        {
            await _flagAnimator.PlayRemove(lifetime);
            _flagAnimator.gameObject.SetActive(false);
        }
    }
}