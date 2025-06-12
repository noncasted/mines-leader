namespace Backend.Users;

public interface IUserProgressionRecord
{
    Guid Id { get; }
    DateTime Date { get; }

    int GetExperience();
}