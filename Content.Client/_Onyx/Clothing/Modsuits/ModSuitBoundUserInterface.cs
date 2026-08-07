using Content.Shared._Onyx.Clothing.Modsuits;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Clothing.Modsuits;

[UsedImplicitly]
public sealed class ModSuitBoundUserInterface : BoundUserInterface
{
    private ModSuitMenu? _menu;

    public ModSuitBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ModSuitMenu>();
        _menu.SetEntity(Owner);

        _menu.EjectBatteryButtonPressed += () =>
        {
            SendPredictedMessage(new ModSuitEjectBatteryBuiMessage());
        };

        _menu.RemoveModuleButtonPressed += module =>
        {
            SendPredictedMessage(new ModSuitRemoveModuleBuiMessage(EntMan.GetNetEntity(module)));
        };
    }

    public override void Update()
    {
        _menu?.UpdateUI();
    }
}
