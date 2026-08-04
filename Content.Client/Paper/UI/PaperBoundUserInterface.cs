using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Content.Shared.Paper;
using static Content.Shared.Paper.PaperComponent;
using Content.Client._Onyx.Language.Paper; // <Onyx-PaperLanguages>
using Content.Shared._Onyx.Language.Paper; // <Onyx-PaperLanguages>

namespace Content.Client.Paper.UI;

[UsedImplicitly]
public sealed class PaperBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PaperWindow? _window;

    public PaperBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PaperWindow>();
        _window.OnLanguageSaved += InputOnTextEntered; // <Onyx-PaperLanguages-edited>

        if (EntMan.TryGetComponent<PaperComponent>(Owner, out var paper))
        {
            _window.MaxInputLength = paper.ContentSize;
        }
        if (EntMan.TryGetComponent<PaperVisualsComponent>(Owner, out var visuals))
        {
            _window.InitVisuals(Owner, visuals);
        }
        EntMan.System<PaperLanguageViewSystem>().PopulatePrefetched(Owner, _window); // <Onyx-PaperLanguages>
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        _window?.Populate((PaperBoundUserInterfaceState) state);
    }

    // <Onyx-PaperLanguages>
    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is PaperLanguageViewMessage view)
        {
            EntMan.System<PaperLanguageViewSystem>().Store(Owner, view); // <Onyx-PaperLanguages>
            PopulateLanguage(view);
        }
    }
    // </Onyx-PaperLanguages>

    public void PopulateLanguage(PaperLanguageViewMessage view) => _window?.PopulateLanguage(view); // <Onyx-PaperLanguages>

    private void InputOnTextEntered(uint revision, ulong viewGeneration, List<PaperLanguageEditOperation> operations) // <Onyx-PaperLanguages-edited>
    {
        SendMessage(new PaperLanguageSaveMessage(revision, viewGeneration, operations)); // <Onyx-PaperLanguages-edited>
    }
}
