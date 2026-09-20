using Internal;
using NaughtyAttributes;
using UnityEngine;

namespace GamePlay.Boards
{
    public class BoardsRevealOptions : EnvAsset
    {
        [SerializeField] [Min(0f)] private float _explosionTime = 2f;

        /// <summary>
        /// По оси X — нормализованное время подрыва, по оси Y — доля уже взорванных мин.
        /// Чем круче кривая, тем больше мин взрывается одновременно.
        /// </summary>
        [SerializeField] [CurveRange(0, 0, 1, 1)]
        private AnimationCurve _explodedShareCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField] [Min(0f)] private float _delayBeforeResults = 0.5f;

        public float ExplosionTime => _explosionTime;
        public AnimationCurve ExplodedShareCurve => _explodedShareCurve;
        public float DelayBeforeResults => _delayBeforeResults;
    }
}
