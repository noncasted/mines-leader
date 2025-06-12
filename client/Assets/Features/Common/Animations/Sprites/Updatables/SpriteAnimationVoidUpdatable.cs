namespace Common.Animations
{
    public class SpriteAnimationVoidUpdatable : IUpdatableSpriteAnimation
    {
        public SpriteAnimationVoidUpdatable(ISpriteAnimationRenderer renderer, IFrameProvider frameProvider)
        {
            _renderer = renderer;
            _frameProvider = frameProvider;
        }

        private readonly ISpriteAnimationRenderer _renderer;
        private readonly IFrameProvider _frameProvider;

        private float _frameTime;
        private float _timer;
        private int _index;

        public void Start(float time)
        {
            _frameTime = time / _frameProvider.FrameCount;
            _index = 0;
            _timer = 0;
        }

        public bool Update(float deltaTime)
        {
            var sprite = _frameProvider.GetFrame(_index);
            _renderer.SetSprite(sprite);

            _timer += deltaTime;

            if (_timer < _frameTime)
                return false;

            _timer = 0f;
            _index++;

            if (_index < _frameProvider.FrameCount)
                return false;

            return true;
        }

        public void Dispose()
        {
        }
    }
}