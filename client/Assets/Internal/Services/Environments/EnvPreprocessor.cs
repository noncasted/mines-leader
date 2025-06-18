namespace Internal
{
    public abstract class EnvPreprocessor : EnvAsset, IEnvAssetKeyOverride
    {
        public abstract void Execute();

        public string GetKeyOverride()
        {
            return typeof(EnvPreprocessor).FullName;
        }
    }
}