namespace Common.Animations
{
    public class SpriteAnimationLoopUpdatable : IUpdatableSpriteAnimation
    {
        public SpriteAnimationLoopUpdatable(SpriteAnimationVoidUpdatable voidUpdatable)
        {
            _voidUpdatable = voidUpdatable;
        }

        private readonly SpriteAnimationVoidUpdatable _voidUpdatable;
        
        private float _time;

        public void Start(float time)
        {
            _time = time;
            _voidUpdatable.Start(_time);
        }

        public bool Update(float deltaTime)
        {
            if (_voidUpdatable.Update(deltaTime) == false)
                return false;

            _voidUpdatable.Start(_time);

            return false;
        }

        public void Dispose()
        {
            
        }
    }
}