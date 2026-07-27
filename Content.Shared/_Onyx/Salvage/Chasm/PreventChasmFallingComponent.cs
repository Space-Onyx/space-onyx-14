namespace Content.Shared._Onyx.Salvage.Chasm;

[RegisterComponent]
public sealed partial class PreventChasmFallingComponent : Component
{
    [DataField]
    public bool DeleteOnUse = true;
}
