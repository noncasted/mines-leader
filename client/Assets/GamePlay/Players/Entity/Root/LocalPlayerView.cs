using Internal;
using UnityEngine;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class LocalPlayerView : ScopeEntityView, ISceneService
    {
        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this);
        }
    }
}