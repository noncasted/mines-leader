namespace Infrastructure;

public class DbGrainReaderSelect
{
    public bool Id { get; set; }
    public bool Value { get; set; }
    public bool Extension { get; set; }

    public string FormQuery()
    {
        var entries = new List<string>();

        if (Id == true)
            entries.Add("key");

        if (Value == true)
            entries.Add("value");

        if (Extension == true)
            entries.Add("extension");

        if (entries.Count == 0)
            entries.Add("*");

        return string.Join(", ", entries);
    }

    public void Validate()
    {
        if (Id == false && Value == false && Extension == false)
        {
            Id = true;
            Value = true;
            Extension = true;
        }
    }
}