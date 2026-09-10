namespace Internal
{
    public interface IProvides<T>
    {
        void Construct(T target);
    }
}
