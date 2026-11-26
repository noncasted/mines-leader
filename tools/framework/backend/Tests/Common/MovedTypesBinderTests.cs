using Common.Extensions;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Framework.Tests.Common;

public class MovedTypesBinderTests
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.All,
        SerializationBinder = new MovedTypesBinder()
    };

    [Fact]
    public void Type_InAssemblyThatNoLongerExists_IsFoundByFullName()
    {
        var json = """{"$type":"Framework.Tests.Common.MovedState, GoneAssembly","Value":7}""";

        var state = JsonConvert.DeserializeObject<MovedState>(json, Settings);

        state!.Value.Should().Be(7);
    }

    [Fact]
    public void Type_MovedOutOfExistingAssembly_IsFoundByFullName()
    {
        // Common exists but no longer declares the type.
        var json = """{"$type":"Framework.Tests.Common.MovedState, Common","Value":3}""";

        var state = JsonConvert.DeserializeObject<MovedState>(json, Settings);

        state!.Value.Should().Be(3);
    }

    [Fact]
    public void GenericArgument_InAssemblyThatNoLongerExists_IsFoundByFullName()
    {
        var json = """
                   {"$type":"System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[Framework.Tests.Common.MovedState, GoneAssembly]], System.Private.CoreLib",
                    "a":{"$type":"Framework.Tests.Common.MovedState, GoneAssembly","Value":1}}
                   """;

        var states = JsonConvert.DeserializeObject<Dictionary<string, MovedState>>(json, Settings);

        states!["a"].Value.Should().Be(1);
    }

    [Fact]
    public void UnknownType_Throws()
    {
        var json = """{"$type":"Nowhere.Missing, GoneAssembly","Value":1}""";

        var act = () => JsonConvert.DeserializeObject<object>(json, Settings);

        act.Should().Throw<JsonSerializationException>();
    }

    [Fact]
    public void WrittenNames_AreUnchanged()
    {
        var json = JsonConvert.SerializeObject(new MovedState { Value = 5 }, Settings);

        json.Should().Contain("\"$type\":\"Framework.Tests.Common.MovedState, Framework.Tests\"");
    }
}
