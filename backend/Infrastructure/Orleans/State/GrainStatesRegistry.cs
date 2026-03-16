using Common;

namespace Infrastructure.State;

public class GrainStateInfo
{
    public required string TableName { get; init; }
    public required GrainKeyType KeyType { get; init; }
    public required Type Type { get; init; }
}

public class TypeAliasAttribute : Attribute
{
    public TypeAliasAttribute(string alias)
    {
        Alias = alias;
    }
    
    public string Alias { get; }
}

public interface IGrainStatesRegistry
{
    IReadOnlyDictionary<string, GrainStateInfo> States { get; }
}

public class GrainStatesRegistry : IGrainStatesRegistry
{
    public GrainStatesRegistry(ICollection<GrainStateInfo> statesInfo)
    {
        var states = new Dictionary<string, GrainStateInfo>(statesInfo.Count);
        States = states;
        
        foreach (var stateInfo in statesInfo)
        {
            var stateName = stateInfo.Type.FullName!;
            states.Add(stateName, stateInfo);
        }
    }

    public IReadOnlyDictionary<string, GrainStateInfo> States { get; }
}