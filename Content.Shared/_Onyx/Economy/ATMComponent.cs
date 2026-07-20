using System;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Economy;

[RegisterComponent]
public sealed partial class ATMComponent : Component
{
    [DataField("idCardSlot")]
    public ItemSlot CardSlot = new ();

    [DataField("currencyType")]
    public string CurrencyType = "SpaceCash";

    public string SlotId = "IdCardSlot";

    [DataField]
    public ProtoId<StackPrototype> CreditStackPrototype = "Credit";

    [DataField("soundInsertCurrency")]
    public SoundSpecifier SoundInsertCurrency = new SoundPathSpecifier("/Audio/_Onyx/Machines/polaroid2.ogg");

    [DataField("soundWithdrawCurrency")]
    public SoundSpecifier SoundWithdrawCurrency = new SoundPathSpecifier("/Audio/_Onyx/Machines/polaroid1.ogg");

    [DataField("soundApply")]
    public SoundSpecifier SoundApply = new SoundPathSpecifier("/Audio/_Onyx/Machines/chime.ogg");

    [DataField("soundDeny")]
    public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/_Onyx/Machines/buzz-sigh.ogg");

    [DataField("emagDuration")]
    public float EmagDuration = 30f;

    [DataField]
    public TimeSpan? EmaggedUntil;
}

[Serializable, NetSerializable]
public enum ATMUiKey
{
    Key
}
