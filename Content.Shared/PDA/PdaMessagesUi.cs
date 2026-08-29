using Robust.Shared.Serialization;

namespace Content.Shared.PDA;

[Serializable, NetSerializable]
public sealed class PdaToggleFlashlightMessage : BoundUserInterfaceMessage
{
    public PdaToggleFlashlightMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaShowRingtoneMessage : BoundUserInterfaceMessage
{
    public PdaShowRingtoneMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaShowUplinkMessage : BoundUserInterfaceMessage
{
    public PdaShowUplinkMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaLockUplinkMessage : BoundUserInterfaceMessage
{
    public PdaLockUplinkMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaShowMusicMessage : BoundUserInterfaceMessage
{
    public PdaShowMusicMessage() { }
}

// <Onyx-PdaPower>
[Serializable, NetSerializable]
public sealed class PdaPowerOffMessage : BoundUserInterfaceMessage
{
    public PdaPowerOffMessage() { }
}
// </Onyx-PdaPower>

[Serializable, NetSerializable]
public sealed class PdaRequestUpdateInterfaceMessage : BoundUserInterfaceMessage
{
    public PdaRequestUpdateInterfaceMessage() { }
}

// <Onyx-PdaTheme>
[Serializable, NetSerializable]
public sealed class PdaSetThemeMessage : BoundUserInterfaceMessage
{
    public Color Accent;

    public PdaSetThemeMessage() { }

    public PdaSetThemeMessage(Color accent)
    {
        Accent = accent;
    }
}
// </Onyx-PdaTheme>
