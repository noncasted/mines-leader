using System.Collections.Generic;
using System.Linq;
using GamePlay.Prefabs;
using Internal;
using Tools.PrefabBuilder;
using UnityEngine;
using VContainer;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class PlayerManaView : MonoBehaviour, IEntityComponent, IScopeLoaded
    {
        [SerializeField] private Transform _root;
        [SerializeField] private float _spacing = 0.25f;

        private IPlayerMana _mana;
        private GamePrefabs _prefabs;

        [Inject]
        internal void Construct(IPlayerMana mana, GamePrefabs prefabs)
        {
            _prefabs = prefabs;
            _mana = mana;
        }

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IScopeLoaded>();
        }

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _mana.Current.View(lifetime, _ => Recalculate());
            _mana.BaseMax.View(lifetime, _ => Recalculate());
            _mana.ResultMax.View(lifetime, _ => Recalculate());
        }

        private void Recalculate()
        {
            var current = _mana.Current.Value;
            var baseMax = _mana.BaseMax.Value;
            var resultMax = _mana.ResultMax.Value;

            var points = _root.CreateRequiredFromPrefab(_prefabs.ManaPoint, resultMax);
            points = points.Reverse().ToList();

            LayoutPoints(points);

            for (int i = 0; i < points.Count; i++)
            {
                if (i < baseMax)
                    points[i].SetBase();
                else
                    points[i].SetAdditional();

                if (i < current)
                    points[i].SetFull();
                else
                    points[i].SetEmpty();
            }
        }

        private void LayoutPoints(IReadOnlyList<PlayerManaPointView> points)
        {
            for (int i = 0; i < points.Count; i++)
            {
                points[i].transform.localPosition = new Vector3(-i * _spacing, 0f, 0f);
            }
        }
    }
}