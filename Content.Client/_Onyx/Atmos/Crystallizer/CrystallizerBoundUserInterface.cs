using Content.Shared._Onyx.Atmos.Crystallizer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Atmos.Crystallizer;

[UsedImplicitly]
public sealed class CrystallizerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CrystallizerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CrystallizerWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.ToggleStatusButton.OnToggled += _ =>
        {
            SendMessage(new CrystallizerToggleMessage());
        };
        _window.OnRecipeSelected += recipe => SendMessage(new CrystallizerSelectRecipeMessage(recipe));
        _window.OnGasInputChanged += input => SendMessage(new CrystallizerSetGasInputMessage(input));
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window is null)
            return;
        switch (message)
        {
            case CrystallizerUpdateGasMixtureMessage gas:
                _window.SetGasMixture(gas.GasMixture);
                break;
            case CrystallizerProgressBarMessage progress:
                _window.SetProgressBar(progress.ProgressBar);
                break;
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window is null || state is not CrystallizerBoundUserInterfaceState crystallizer)
            return;
        _window.SetActive(crystallizer.Enabled);
        _window.SelectRecipeById(crystallizer.SelectedRecipeId);
        _window.GasInput.Value = crystallizer.GasInput;
        _window.SetGasMixture(crystallizer.GasMixture);
        _window.SetProgressBar(crystallizer.ProgressBar);
    }
}
