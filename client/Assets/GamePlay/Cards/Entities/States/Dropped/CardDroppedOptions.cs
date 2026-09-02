using Internal;
using NaughtyAttributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Cards
{
    [InlineEditor]
    public class CardDroppedOptions : EnvAsset
    {
        [SerializeField] private float _time;

        [SerializeField] [CurveRange] private AnimationCurve _fadeCurve;

        [SerializeField] private Color _spritesColor = new(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color _nameColor = new(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color _descriptionColor = new(0.55f, 0.55f, 0.55f, 1f);

        public float Time => _time;
        public Curve FadeCurve => new(_time, _fadeCurve);
        public Color SpritesColor => _spritesColor;
        public Color NameColor => _nameColor;
        public Color DescriptionColor => _descriptionColor;
    }
}
