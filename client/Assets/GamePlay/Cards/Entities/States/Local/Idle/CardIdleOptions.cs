using Internal;
using NaughtyAttributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Cards
{
    [InlineEditor]
    public class CardIdleOptions : EnvAsset
    {
        [SerializeField] private Curve _selectionCurve;
        [SerializeField][CurveRange(0,1,1,2)] private AnimationCurve _scaleCurve;
        [SerializeField] private float _selectionDistance;
        [SerializeField] private float _selectionForce;
        
        public Curve SelectionCurve => _selectionCurve;
        public AnimationCurve ScaleCurve => _scaleCurve;
        public float SelectionDistance => _selectionDistance;
        public float SelectionForce => _selectionForce;
    }
}