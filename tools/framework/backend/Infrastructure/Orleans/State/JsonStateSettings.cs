using Common.Extensions;
using Newtonsoft.Json;
using Orleans.GrainReferences;

namespace Infrastructure.State;

public static class JsonStateSettings
{
    public static JsonSerializerSettings CreateBase()
    {
        return new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            // Include, not Ignore: with Ignore any value equal to the type default (0, false)
            // was dropped from the JSON on write and replaced by the property initializer on read.
            // Because of that a setting could not be set to 0 - it silently fell back to the class default.
            DefaultValueHandling = DefaultValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            Formatting = Formatting.None,
            SerializationBinder = new MovedTypesBinder(),
        };
    }
}
