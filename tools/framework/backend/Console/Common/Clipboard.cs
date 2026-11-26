using BlazorBlueprint.Components;
using Microsoft.JSInterop;

namespace Console;

public interface IClipboard
{
    Task Copy(string text);
}

public class Clipboard : IClipboard
{
    public Clipboard(IJSRuntime js, ToastService toast)
    {
        _js = js;
        _toast = toast;
    }

    private readonly IJSRuntime _js;
    private readonly ToastService _toast;

    public async Task Copy(string text)
    {
        try
        {
            await _js.InvokeVoidAsync("navigator.clipboard.writeText", text);
            _toast.Success("Copied to clipboard", "Done");
        }
        catch (JSException exception)
        {
            // The browser refuses the clipboard (no permission, an insecure page): the user is told, nothing else depends on it.
            _toast.Error($"Failed to copy: {exception.Message}", "Error");
        }
    }
}
