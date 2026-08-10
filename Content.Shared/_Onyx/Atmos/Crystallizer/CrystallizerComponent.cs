using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Atmos.Crystallizer;

[RegisterComponent]
public sealed partial class CrystallizerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public string? SelectedRecipeId;

    [ViewVariables(VVAccess.ReadWrite)]
    public float GasInput;

    [DataField("inlet")]
    public string InletName = "inlet";

    [DataField("regulator")]
    public string RegulatorName = "regulator";

    [ViewVariables(VVAccess.ReadWrite)]
    public GasMixture CrystallizerGasMixture = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public float ProgressBar;

    [ViewVariables(VVAccess.ReadWrite)]
    public float QualityLoss;

    [ViewVariables]
    public float TotalRecipeMoles;
}

[Serializable, NetSerializable]
public enum CrystallizerUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class CrystallizerToggleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CrystallizerSelectRecipeMessage(string? recipeId) : BoundUserInterfaceMessage
{
    public string? RecipeId { get; } = recipeId;
}

[Serializable, NetSerializable]
public sealed class CrystallizerSetGasInputMessage(float gasInput) : BoundUserInterfaceMessage
{
    public float GasInput { get; } = gasInput;
}

[Serializable, NetSerializable]
public sealed class CrystallizerBoundUserInterfaceState(
    bool enabled,
    string? selectedRecipeId,
    float gasInput,
    GasMixture gasMixture,
    float progressBar,
    float qualityLoss) : BoundUserInterfaceState
{
    public bool Enabled { get; } = enabled;
    public string? SelectedRecipeId { get; } = selectedRecipeId;
    public float GasInput { get; } = gasInput;
    public GasMixture GasMixture { get; } = gasMixture;
    public float ProgressBar { get; } = progressBar;
    public float QualityLoss { get; } = qualityLoss;
}

[Serializable, NetSerializable]
public sealed class CrystallizerUpdateGasMixtureMessage(GasMixture gasMixture) : BoundUserInterfaceMessage
{
    public GasMixture GasMixture { get; } = gasMixture;
}

[Serializable, NetSerializable]
public sealed class CrystallizerProgressBarMessage(float progressBar) : BoundUserInterfaceMessage
{
    public float ProgressBar { get; } = progressBar;
}
