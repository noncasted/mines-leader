using System.Text;

namespace Infrastructure;

public static class DbGrainReaderExtensions
{
    public static DbGrainReader<TState> CreateDbReader<TState>(this IOrleans orleans, string table)
    {
        return new DbGrainReader<TState>(orleans);
    }

    public static TState Deserialize<TState>(this DbGrainReader<TState> reader, DbGrainEntry entry) where TState : class
    {
        var stringPayload = Encoding.UTF8.GetString(entry.Value);
        var value = reader.Orleans.Serializer.Deserialize(typeof(TState), stringPayload) as TState;
        return value!;
    }


    public static DbGrainReader<TState> WhereType<TState>(this DbGrainReader<TState> reader, string type)
    {
        reader.Where.Type = type;
        return reader;
    }

    public static DbGrainReader<TState> WhereExtension<TState>(this DbGrainReader<TState> reader, string extension)
    {
        reader.Where.Extension = extension;
        return reader;
    }

    public static DbGrainReader<TState> SelectID<TState>(this DbGrainReader<TState> reader)
    {
        reader.Select.Id = true;
        return reader;
    }

    public static DbGrainReader<TState> SelectPayload<TState>(this DbGrainReader<TState> reader)
    {
        reader.Select.Value = true;
        return reader;
    }

    public static DbGrainReader<TState> SelectExtension<TState>(this DbGrainReader<TState> reader)
    {
        reader.Select.Extension = true;
        return reader;
    }
}