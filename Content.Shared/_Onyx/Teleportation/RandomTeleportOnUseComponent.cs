using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Teleportation;

[RegisterComponent, NetworkedComponent]
public sealed partial class RandomTeleportOnUseComponent : Component
{
    [DataField]
    public Vector2 Radius = new(10f, 20f);

    [DataField]
    public int TeleportAttempts = 20;

    [DataField]
    public bool ConsumeOnUse = true;

    [DataField]
    public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
}
