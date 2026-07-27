using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Weapons.Multishot;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MultishotComponent : Component
{
    [DataField, AutoNetworkedField] public bool MultishotAffected;
    [DataField] public float MissChance = 0.2f;
    [DataField] public float SpreadMultiplier = 1.5f;
    [DataField] public float SpreadAddition = 5f;
    [DataField] public float HandDamageAmount;
    [DataField] public string HandDamageType = "Blunt";
    [DataField] public float StaminaDamage;
    [DataField] public string ExamineMessage = "multishot-component-examine";
}
