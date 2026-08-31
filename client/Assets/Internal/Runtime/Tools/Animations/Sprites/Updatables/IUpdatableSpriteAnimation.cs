namespace Internal
{
    public interface IUpdatableSpriteAnimation
    {
        bool Update(float deltaTime);
        void Dispose();
    }
}