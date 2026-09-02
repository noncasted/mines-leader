using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GamePlay.Services
{
    public class GameFloatingTextBuilder
    {
        public GameFloatingTextBuilder(IGameFloatingText service, GameFloatingTextRequest request)
        {
            _service = service;
            _request = request;
        }

        private readonly IGameFloatingText _service;

        private GameFloatingTextRequest _request;

        public GameFloatingTextBuilder At(Vector3 position)
        {
            _request.Position = position;
            return this;
        }

        public GameFloatingTextBuilder At(Vector2 position)
        {
            _request.Position = position;
            return this;
        }

        public GameFloatingTextBuilder At(Transform target)
        {
            _request.Position = target.position;
            return this;
        }

        public GameFloatingTextBuilder WithText(string text)
        {
            _request.Text = text;
            return this;
        }

        public GameFloatingTextBuilder WithText(string text, Color color)
        {
            _request.Text = text;
            _request.TextColor = color;
            return this;
        }

        public GameFloatingTextBuilder WithTextColor(Color color)
        {
            _request.TextColor = color;
            return this;
        }

        public GameFloatingTextBuilder WithIcon(Sprite icon)
        {
            _request.Icon = icon;
            return this;
        }

        public GameFloatingTextBuilder WithIcon(Sprite icon, Color color)
        {
            _request.Icon = icon;
            _request.IconColor = color;
            return this;
        }

        public GameFloatingTextBuilder WithIconColor(Color color)
        {
            _request.IconColor = color;
            return this;
        }

        public GameFloatingTextBuilder WithDirection(Vector3 direction)
        {
            _request.Direction = direction.normalized;
            return this;
        }

        public GameFloatingTextBuilder WithDistance(float distance)
        {
            _request.Distance = distance;
            return this;
        }

        public GameFloatingTextBuilder WithTime(float time)
        {
            _request.Time = time;
            return this;
        }

        public GameFloatingTextBuilder WithDelay(float delay)
        {
            _request.Delay = delay;
            return this;
        }

        public GameFloatingTextBuilder WithScale(float scale)
        {
            _request.Scale = scale;
            return this;
        }

        public GameFloatingTextBuilder WithMoveCurve(AnimationCurve curve)
        {
            _request.Move = curve;
            return this;
        }

        public GameFloatingTextBuilder WithMoveCurve(AnimationCurve curve, float time)
        {
            _request.Move = curve;
            _request.Time = time;
            return this;
        }

        public GameFloatingTextBuilder WithFadeCurve(AnimationCurve curve)
        {
            _request.Fade = curve;
            return this;
        }

        public GameFloatingTextBuilder WithSizeCurve(AnimationCurve curve)
        {
            _request.Size = curve;
            return this;
        }

        public GameFloatingTextBuilder WithPrefab(GameFloatingTextView prefab)
        {
            _request.Prefab = prefab;
            return this;
        }

        public UniTask Run()
        {
            return _service.Run(_request);
        }

        public void RunDetached()
        {
            _service.Run(_request).Forget();
        }
    }
}
