using System.Linq;
using Internal;
using Tools;
using UnityEngine;
using VContainer;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class PlayerManaView : MonoBehaviour, IEntityComponent, IScopeLoaded
    {
        [SerializeField] private Transform _root;

        private IPlayerMana _mana;

        [Inject]
        private void Construct(IPlayerMana mana)
        {
            _mana = mana;
        }

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeLoaded>();
        }

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _mana.Current.View(lifetime, current =>
                {
                    var points = _root.CreateRequiredFromPrefab(Prefabs.ManaPoint.As<PlayerManaPointView>(),
                        _mana.Max.Value
                    );
                    points = points.Reverse().ToList();

                    for (int i = 0; i < points.Count; i++)
                    {
                        if (i < current)
                            points[i].SetFull();
                        else
                            points[i].SetEmpty();
                    }
                }
            );
        }
    }
}