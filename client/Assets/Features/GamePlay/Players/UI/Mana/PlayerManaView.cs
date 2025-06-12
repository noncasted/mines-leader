using System.Collections.Generic;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class PlayerManaView : MonoBehaviour, IEntityComponent, IScopeLoaded
    {
        [SerializeField] private PlayerManaPointView _pointPrefab;
        [SerializeField] private Transform _root;

        private readonly List<PlayerManaPointView> _points = new();

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
            for (int i = 0; i < _mana.Max.Value; i++)
            {
                var point = Instantiate(_pointPrefab, _root);
                _points.Add(point);
            }
            
            _points.Reverse();

            _mana.Current.View(lifetime, current =>
            {
                for (int i = 0; i < _points.Count; i++)
                {
                    if (i < current)
                        _points[i].SetFull();
                    else
                        _points[i].SetEmpty();
                }
            });
        }
    }
}