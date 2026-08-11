using Content.Shared._Onyx.CustomLawboard;
using Content.Shared.Silicons.Laws;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.CustomLawboard;

[UsedImplicitly]
public sealed class CustomLawboardBoundInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private LawboardSiliconLawUi? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<LawboardSiliconLawUi>();
        _window.LawsSaved += laws => SendMessage(new CustomLawboardChangeLawsMessage(laws));

        if (EntMan.TryGetComponent<CustomLawboardComponent>(Owner, out var component))
            Update((Owner, component));
    }

    public void Update(Entity<CustomLawboardComponent> ent)
    {
        _window?.SetLaws(ent.Comp.Laws);
    }
}
