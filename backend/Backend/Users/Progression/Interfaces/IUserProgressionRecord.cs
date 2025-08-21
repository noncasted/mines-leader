namespace Backend.Users;

public interface IUserProgressionRecord
{
    DateTime Date { get; }

    int GetExperience();
}