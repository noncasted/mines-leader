using GamePlay.Services;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Cheats
{
    [DisallowMultipleComponent]
    public class GameCheatsUI : MonoBehaviour, ISceneService, IScopeSetup
    {
        private IGameInput _input;

        [Inject]
        private void Construct(IGameInput input)
        {
            _input = input;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IScopeSetup>();

            gameObject.SetActive(false);
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _input.Cheats.Advise(lifetime, () => gameObject.SetActive(!gameObject.activeSelf));
        }
    }
}