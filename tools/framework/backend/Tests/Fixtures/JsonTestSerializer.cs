using Infrastructure;
using Infrastructure.State;
using Internal;
using Newtonsoft.Json;

namespace Framework.Tests.Fixtures;

// Plain Json.NET in place of StateSerializer, which needs the grain reference activator of a silo.
public class JsonTestSerializer : IStateSerializer
{
    public string Serialize(object value) => JsonConvert.SerializeObject(value);
    public string Serialize<T>(T value) => JsonConvert.SerializeObject(value);
    public T Deserialize<T>(string value) => JsonConvert.DeserializeObject<T>(value)!;
    public T? TryDeserialize<T>(string value) => JsonConvert.DeserializeObject<T>(value);
    public object? Deserialize(string value, Type type) => JsonConvert.DeserializeObject(value, type);
}
