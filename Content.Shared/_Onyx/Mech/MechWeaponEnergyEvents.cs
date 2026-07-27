namespace Content.Shared._Onyx.Mech;

[ByRefEvent]
public record struct GetMechWeaponChargeEvent(float CurrentCharge = 0f, float MaxCharge = 0f, bool Handled = false);

[ByRefEvent]
public record struct ChangeMechWeaponChargeEvent(float Amount, bool Handled = false, bool Changed = false);
