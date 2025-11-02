namespace Meta.Users;

public interface IUserProgressionRecord
{
    DateTime Date { get; }

    int GetExperience();
}