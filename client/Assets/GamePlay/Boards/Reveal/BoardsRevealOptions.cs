using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    public class BoardsRevealOptions : EnvAsset
    {
        [SerializeField][Min(0f)] private float _explosionTime = 2f;

        /// <summary>
        /// X axis - normalized detonation time, Y axis - share of mines already blown up.
        /// The steeper the curve, the more mines explode at once.
        /// </summary>
        [SerializeField]
        [CurveRange(0, 0, 1, 1)]
        private AnimationCurve _explodedShareCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField][Min(0f)] private float _delayBeforeResults = 0.5f;

        public float ExplosionTime => _explosionTime;
        public AnimationCurve ExplodedShareCurve => _explodedShareCurve;
        public float DelayBeforeResults => _delayBeforeResults;
    }
}
