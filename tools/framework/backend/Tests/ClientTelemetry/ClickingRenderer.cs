using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;

namespace Framework.Tests.ClientTelemetry;

#pragma warning disable BL0006 // the render tree is the only way to reach a handler without a browser

// Renders a page and presses its first button: the static HtmlRenderer has no events.
public sealed class ClickingRenderer : Renderer
{
    public ClickingRenderer(IServiceProvider services) : base(services, NullLoggerFactory.Instance)
    {
    }

    private readonly List<ulong> _clicks = [];

    private int _pageId;

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    public Task<IComponent> RenderPage(Type page, Dictionary<string, object?> parameters)
    {
        return Dispatcher.InvokeAsync(async () =>
        {
            var component = InstantiateComponent(page);
            _pageId = AssignRootComponentId(component);
            await RenderRootComponentAsync(_pageId, ParameterView.FromDictionary(parameters));
            return component;
        });
    }

    // New parameters for the page already rendered, as the router gives them on a link to the same route.
    public Task SetPageParameters(Dictionary<string, object?> parameters)
    {
        return Dispatcher.InvokeAsync(() => RenderRootComponentAsync(_pageId, ParameterView.FromDictionary(parameters)));
    }

    public Task ClickFirstButton()
    {
        return Dispatcher.InvokeAsync(() => DispatchEventAsync(_clicks[0], null, new MouseEventArgs()));
    }

    // Calls a private handler of the page the way an event does: a render after its synchronous part and after its end.
    public Task Handle(IComponent component, string handler)
    {
        var method = component.GetType().GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var render = typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return Dispatcher.InvokeAsync(async () =>
        {
            var task = (Task)method.Invoke(component, null)!;
            render.Invoke(component, null);
            await task;
            render.Invoke(component, null);
        });
    }

    protected override void HandleException(Exception exception)
    {
        throw exception;
    }

    protected override Task UpdateDisplayAsync(in RenderBatch batch)
    {
        for (var index = 0; index < batch.ReferenceFrames.Count; index++)
        {
            var frame = batch.ReferenceFrames.Array[index];

            if (frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick" && frame.AttributeEventHandlerId != 0)
            {
                _clicks.Add(frame.AttributeEventHandlerId);
            }
        }

        return Task.CompletedTask;
    }
}
