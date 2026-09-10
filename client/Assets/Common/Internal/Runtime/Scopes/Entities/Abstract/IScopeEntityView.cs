namespace Internal
{
    public interface IScopeEntityView
    {
        void CreateViews(IEntityBuilder builder);
        void Bind(IContainer container);
    }
}