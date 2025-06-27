using Microsoft.AspNetCore.Components;

namespace Management.Web;

public interface IConsoleNavigation
{
    void NavigateTo(string path);
}

public class ConsoleNavigation : IConsoleNavigation
{
    public ConsoleNavigation(NavigationManager manager)
    {
        _manager = manager;
    }

    private readonly NavigationManager _manager;

    public void NavigateTo(string path)
    {
        _manager.NavigateTo(path);
    }
}