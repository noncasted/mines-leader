using Cysharp.Threading.Tasks;

namespace Common.Animations
{
    public class SpriteAnimationAsyncUpdatable : IUpdatableSpriteAnimation
    {
        public SpriteAnimationAsyncUpdatable(SpriteAnimationVoidUpdatable voidUpdatable)
        {
            _voidUpdatable = voidUpdatable;
        }

        private readonly SpriteAnimationVoidUpdatable _voidUpdatable;

        private float _time;
        private UniTaskCompletionSource _completion;

        public UniTask Process(float time)
        {
            _time = time;
            _voidUpdatable.Start(_time);

            _completion?.TrySetCanceled();
            _completion = new UniTaskCompletionSource();

            return _completion.Task;
        }

        public bool Update(float deltaTime)
        {
            if (_voidUpdatable.Update(deltaTime) == false)
                return false;

            _completion.TrySetResult();
            _completion = null;
            
            return false;
        }

        public void Dispose()
        {
            _completion?.TrySetCanceled();
            _completion = null;
        }
    }
}