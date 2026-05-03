using System.Collections.Generic;
using System.Linq;
using Internal;
using Tools;
using Tools.Runtime.PrefabBuilder;
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
            _mana.Current.View(lifetime, _ => Recalculate());
            _mana.Max.View(lifetime, _ => Recalculate());
        }

        private void Recalculate()
        {
            var current = _mana.Current.Value;
            var max = _mana.Max.Value;

            var points = _root.CreateRequiredFromPrefab(Prefabs.ManaPoint.As<PlayerManaPointView>(), max);
            points = points.Reverse().ToList();

            LayoutPoints(points);

            for (int i = 0; i < points.Count; i++) {
                if (i < current)
                    points[i].SetFull();
                else
                    points[i].SetEmpty();
            }
        }

        private void LayoutPoints(IReadOnlyList<PlayerManaPointView> points)
        {
            for (int i = 0; i < points.Count; i++) {
                points[i].transform.localPosition = new Vector3(-i * _spacing, 0f, 0f);
            }
        }
    }
}