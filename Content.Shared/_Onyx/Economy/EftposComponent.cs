using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Economy;

[RegisterComponent]
public sealed partial class EftposComponent : Component
{
    [ViewVariables]
    public int? BankAccountId;

    [ViewVariables]
    public int Amount;

    [ViewVariables]
    public int? PendingPayerAccountId;

    [ViewVariables]
    public string PendingPayerName = string.Empty;

    [ViewVariables]
    public int? PendingPin;

    [ViewVariables]
    public TimeSpan? PendingTimeout;

    [DataField("soundApply")]
    public SoundSpecifier SoundApply = new SoundPathSpecifier("/Audio/_Onyx/Machines/chime.ogg");

    [DataField("soundDeny")]
    public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/_Onyx/Machines/buzz-sigh.ogg");
}

[Serializable, NetSerializable]
public enum EftposKey
{
    Key
}
