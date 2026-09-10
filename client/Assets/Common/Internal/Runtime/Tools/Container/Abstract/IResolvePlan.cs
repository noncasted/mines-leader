namespace Internal
{
    public interface IResolvePlan
    {
        object Get(int slot);
        T Get<T>(int slot);
    }
}
