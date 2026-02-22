using Newtonsoft.Json;

namespace Common.Extensions;

public static class JsonUtils
{
    private static readonly JsonSerializerSettings _options = new()
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.All,
    };


    public static string Serialize(object value)
    {
        return JsonConvert.SerializeObject(value, _options);
    }

    public static T Deserialize<T>(string raw)
    {
        return JsonConvert.DeserializeObject<T>(raw, _options)!;
    }
}