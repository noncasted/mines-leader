namespace Internal
{
    internal interface IContainerTree
    {
        void AttachChild(IContainer child);
        void DetachChild(IContainer child);
    }
}
