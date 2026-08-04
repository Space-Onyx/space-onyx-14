using Content.Shared._Onyx.Language;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.Language.Paper;

public sealed partial class PaperLanguageTag : IMarkupTagHandler
{
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public string Name => "paperlang";

    public void PushDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        var id = node.Value.StringValue;
        if (id == null || !_prototypes.TryIndex(id, out LanguagePrototype? language))
        {
            context.Font.Push(context.Font.Peek());
            return;
        }

        if (language.Speech.FontId == null && language.Speech.FontSize == null)
        {
            context.Font.Push(context.Font.Peek());
        }
        else
        {
#pragma warning disable CS0618 // Font prototypes are already the language prototype contract.
            var fontNode = language.Speech.FontSize is { } size
                ? new MarkupNode(null, null, new Dictionary<string, MarkupParameter> { ["size"] = new(size) })
                : node;
            var font = FontTag.CreateFont(context.Font, fontNode, _resources, _prototypes,
                language.Speech.FontId ?? FontTag.DefaultFont);
#pragma warning restore CS0618
            context.Font.Push(font);
        }
    }

    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        context.Font.Pop();
    }
}
