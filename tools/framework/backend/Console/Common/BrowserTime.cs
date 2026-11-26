using Microsoft.JSInterop;

namespace Console;

public interface IBrowserTime
{
    // Only after the first render: there is no browser while the page is prerendered.
    Task<BrowserTimeZone> Zone();
}

public class BrowserTime : IBrowserTime
{
    public BrowserTime(IJSRuntime js)
    {
        _js = js;
    }

    private readonly IJSRuntime _js;

    private Task<BrowserTimeZone>? _zone;

    public Task<BrowserTimeZone> Zone()
    {
        return _zone ??= Read();
    }

    private async Task<BrowserTimeZone> Read()
    {
        await using var module = await _js.InvokeAsync<IJSObjectReference>("import", "./_content/Console/browser-time.js");
        var minutes = await module.InvokeAsync<int>("offsetMinutes");

        return new BrowserTimeZone(TimeSpan.FromMinutes(minutes));
    }
}
