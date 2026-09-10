namespace Internal
{
    public interface IContainerBuilderScope : IContainerRegistry
    {
        IContainer Build();
        void AddLoadedAsset(string label, string groupName);
    }
}
