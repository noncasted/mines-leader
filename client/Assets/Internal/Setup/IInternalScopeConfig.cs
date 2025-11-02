namespace Internal
{
    public interface IInternalScopeConfig
    {
        InternalScope Scope { get; }
        IAssetsStorage AssetsStorage { get; }
    }
}