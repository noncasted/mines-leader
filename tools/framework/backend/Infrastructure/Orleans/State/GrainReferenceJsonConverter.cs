using Common.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orleans.GrainReferences;

namespace Infrastructure.State;

public class GrainReferenceJsonConverter : JsonConverter
{
    public GrainReferenceJsonConverter(GrainReferenceActivator activator)
    {
        _activator = activator;
    }

    private static readonly Type AddressableType = typeof(IAddressable);
    private readonly GrainReferenceActivator _activator;

    public override bool CanConvert(Type objectType)
    {
        return AddressableType.IsAssignableFrom(objectType);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {

        GrainReference context = value switch
        {
            GrainReference reference => reference,
            Grain grain => grain.GrainContext.GrainReference,
            IGrainBase grainBase => grainBase.GrainContext.GrainReference,
            _ => throw new InvalidOperationException($"Unsupported type {value?.GetType()} for grain reference")
        };

        var id = context.GrainId;
        var raw = $"{context.InterfaceType.ToString()}:{id.Type.ToString()}:{id.Key.ToString()}";

        writer.WriteValue(raw);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var json = JToken.Load(reader);
        var raw = json.Value<string>()!;
        var split = raw.Split(':', count: 3);

        if (split.Length != 3)
            throw new JsonSerializationException(
                $"[GrainReferenceJsonConverter] Invalid GrainReference format: '{raw}'");

        var encodedInterface = split[0];

        var interfaceType = string.IsNullOrWhiteSpace(encodedInterface)
            ? default
            : GrainInterfaceType.Create(encodedInterface);

        var grainId = GrainId.Create(split[1], split[2]);
        return _activator.CreateReference(grainId, interfaceType);
    }
}
