using Content.Shared._Onyx.Botany.PlantAnalyzer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Botany.PlantAnalyzer;

[UsedImplicitly]
public sealed class PlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PlantAnalyzerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PlantAnalyzerWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.Print.OnPressed += _ => Print();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window != null && message is PlantAnalyzerScannedUserMessage data)
            _window.Populate(data);
    }

    private void Print()
    {
        SendMessage(new PlantAnalyzerPrintMessage());
        if (_window != null)
            _window.Print.Disabled = true;
    }
}
