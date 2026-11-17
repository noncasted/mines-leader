using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Orleans.GrainReferences;

namespace Infrastructure.State;

public interface IStateSerializer
{
    string Serialize(object value);
    string Serialize<T>(T value);
    T Deserialize<T>(string value);
    T? TryDeserialize<T>(string value);
    object? Deserialize(string value, Type type);
}

public class StateSerializer : IStateSerializer
{
    public StateSerializer(IServiceProvider serviceProvider)
    {
        _settings = JsonStateSettings.CreateBase();
        _settings.Converters.Add(new GrainIdConverter());
        _settings.Converters.Add(
            new GrainReferenceJsonConverter(serviceProvider.GetRequiredService<GrainReferenceActivator>()));
    }

    private readonly JsonSerializerSettings _settings;

    public string Serialize(object value)
    {
        return JsonConvert.SerializeObject(value, _settings);
    }

    public string Serialize<T>(T value)
    {
        return JsonConvert.SerializeObject(value, typeof(T), _settings);
    }

    public T Deserialize<T>(string value)
    {
        return JsonConvert.DeserializeObject<T>(value, _settings).ThrowIfNull();
    }

    public T? TryDeserialize<T>(string value)
    {
        return JsonConvert.DeserializeObject<T>(value, _settings);
    }

    public object? Deserialize(string value, Type type)
    {
        return JsonConvert.DeserializeObject(value, type, _settings);
    }
}
