using UnityEngine;

namespace GamePlay.Services
{
    public struct GameFloatingTextRequest
    {
        public Vector3 Position;
        public Vector3 Direction;

        public string Text;
        public Sprite Icon;

        public Color? TextColor;
        public Color? IconColor;

        public float Scale;
        public float Distance;
        public float Time;
        public float Delay;

        public AnimationCurve Move;
        public AnimationCurve Fade;
        public AnimationCurve Size;

        public GameFloatingTextView Prefab;
    }
}
