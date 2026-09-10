namespace Internal
{
    public readonly struct LoadedAssetInfo
    {
        public LoadedAssetInfo(string label, string groupName)
        {
            Label = label;
            GroupName = groupName;
        }

        public readonly string Label;
        public readonly string GroupName;
    }
}