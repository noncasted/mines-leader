using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardRemoteIdleOptions : EnvAsset
    {
        [SerializeField] private Curve _selectionCurve;
        [SerializeField] private float _selectionDistance;
        [SerializeField] private float _selectionForce;

        public Curve SelectionCurve => _selectionCurve;
        public float SelectionDistance => _selectionDistance;
        public float SelectionForce => _selectionForce;
    }
}