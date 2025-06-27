using Common;

namespace Management.Configs;

public interface IConfig
{
    Type Type { get; }
    
    Task Refresh();
    void OnReceived(object value);
}

public interface IConfig<TOptions> : IConfig where TOptions : class, new()
{
    IViewableProperty<TOptions> Value { get; }
}

public class Config<TOptions> : IConfig<TOptions> where TOptions : class, new()
{
    public Config(IOrleans orleans)
    {
        _orleans = orleans;
    }

    private readonly IOrleans _orleans;
    private readonly ViewableProperty<TOptions> _value = new(new TOptions());

    public IViewableProperty<TOptions> Value => _value;

    public Type Type => typeof(TOptions);

    public async Task Refresh()
    {
        var value = await _orleans.GetConfig<TOptions>();
        _value.Set(value);
    }
    
    public void OnReceived(object value)
    {
        _value.Set((TOptions)value);
    }
}