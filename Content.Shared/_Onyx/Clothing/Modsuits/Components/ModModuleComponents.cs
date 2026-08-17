using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Hands.Components;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry;

namespace Content.Shared._Onyx.Clothing.Modsuits.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModModuleContainerComponent : Component
{
    public const string ContainerId = "mod-modules";

    [DataField]
    public int MaxModules = 8;

    [DataField]
    public float InstallDuration = 2f;

    [DataField]
    public float RemoveDuration = 2f;

    /// <summary>
    ///     Modules that are installed into this suit by default when it is created.
    /// </summary>
    [DataField]
    public List<EntProtoId> StartingModules = new();

    [AutoNetworkedField]
    public bool Powered = true;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModModuleComponent : Component
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField]
    public HashSet<string> ConflictTags = new();

    [DataField]
    public ModPart? RequiredPart;

    [DataField]
    public float DrawRate;

    [DataField]
    public bool Permanent;

    [DataField, AutoNetworkedField]
    public bool CanBeDisabled = true;

    [DataField("active"), AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public Dictionary<ModEffectTarget, ComponentRegistry> Effects = new();

    [DataField]
    public List<ModModuleAction> Actions = new();

    [DataField]
    public List<ModIntegratedItem> IntegratedItems = new();

    [AutoNetworkedField]
    public EntityUid? InstalledController;

    [AutoNetworkedField]
    public bool Active;

    [DataField]
    public Dictionary<ModEffectTarget, EntityUid> AppliedTargets = new();

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, EntityUid> ActionEntities = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, EntityUid> ItemEntities = new();

    [DataField]
    public HashSet<int> OwnedUnremoveable = new();
}

[RegisterComponent]
public sealed partial class ModModuleEffectOwnershipComponent : Component
{
    [DataField]
    public Dictionary<string, int> References = new();

    [DataField]
    public HashSet<string> Added = new();
}

[DataDefinition]
public sealed partial class ModModuleAction
{
    [DataField(required: true)]
    public EntProtoId Action;

    [DataField]
    public float UseCost;
}

[RegisterComponent]
public sealed partial class ModModuleStorageComponent : Component
{
    [DataField]
    public List<Box2i> Grid = new();

    [DataField]
    public List<Box2i>? OriginalGrid;

    [DataField]
    public bool ControllerHadStorage;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModModuleLightComponent : Component
{
    [DataField]
    public Color Color = Color.White;

    [DataField]
    public float Energy = 2f;

    [DataField]
    public float Radius = 3f;

    [AutoNetworkedField]
    public bool Enabled;
}

[RegisterComponent]
public sealed partial class ModModuleDispenserComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Prototypes = new();
}

[RegisterComponent]
public sealed partial class ModModuleTeleporterComponent : Component
{
    [DataField]
    public float Radius = 5f;
}

[RegisterComponent]
public sealed partial class ModModuleMagneticComponent : Component;

[RegisterComponent]
public sealed partial class ModModuleGeigerComponent : Component;

[RegisterComponent]
public sealed partial class ModModuleAntiGravityComponent : Component;

[RegisterComponent]
public sealed partial class ModModuleApparatusComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModModuleQuickCarryComponent : Component
{
    [DataField]
    public float Multiplier = 0.33f;

    [AutoNetworkedField]
    public bool Carrying;
}

[RegisterComponent]
public sealed partial class ModModuleArmorBoosterComponent : Component
{
    [DataField(required: true)]
    public DamageModifierSet Modifiers = new();
}

[RegisterComponent]
public sealed partial class ModModuleSpringlockComponent : Component
{
    [DataField] public ReactionMethod LockMethod = ReactionMethod.Touch;
    [DataField] public string TargetReagent = "Water";
    [DataField] public TimeSpan TriggerDelay = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan MusicDelay = TimeSpan.FromSeconds(4);
    [DataField] public DamageSpecifier LockDamage = new()
    {
        DamageDict = { { "Blunt", 20 }, { "Slash", 40 }, { "Piercing", 60 } },
    };
    [DataField] public SoundSpecifier TriggerSound = new SoundPathSpecifier("/Audio/_Onyx/Effects/Modsuit/springlock.ogg");
    [DataField] public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/Items/snap.ogg");
    [DataField] public SoundSpecifier SplatSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
    [DataField] public SoundSpecifier Music = new SoundPathSpecifier("/Audio/_Onyx/Ambience/toreadormarch.ogg");
}

[RegisterComponent]
public sealed partial class ModModuleSpringlockControllerComponent : Component
{
    public EntityUid Module;
}

[RegisterComponent]
public sealed partial class ModModuleSpringlockEffectComponent : Component
{
    public EntityUid Module;
    public bool Triggered;
    public bool Locked;
    public TimeSpan TriggerAt;
    public TimeSpan MusicAt;
    public bool MusicPlayed;
}

[RegisterComponent]
public sealed partial class ModModuleEnergyShieldComponent : Component
{
    [DataField] public EntProtoId Effect = "EnergyShieldEffect";
    [DataField] public int SustainingCount = 5;
}

[RegisterComponent]
public sealed partial class ModModuleEnergyShieldEffectComponent : Component
{
    public EntityUid Module;
    public EntityUid? Effect;
    public int SustainingCount;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RaveOverlayComponent : Component
{
    [DataField, AutoNetworkedField] public Color BaseColor = Color.FromHex("#ff3ce6");
    [DataField, AutoNetworkedField] public Color SecondaryColor = Color.FromHex("#3c9eff");
    [DataField, AutoNetworkedField] public float PulseSpeed = 0.3f;
    [DataField, AutoNetworkedField] public float Intensity = 0.8f;
    [DataField, AutoNetworkedField] public float GrainStrength = 0.25f;
    [DataField, AutoNetworkedField] public float Distortion = 0.15f;
}

[RegisterComponent]
public sealed partial class ModModuleTanningComponent : Component;

[RegisterComponent]
public sealed partial class ModModuleAtrocinatorComponent : Component
{
    [DataField] public float Radius = 5f;
    [DataField] public float ThrowStrength = 20f;
    [DataField] public TimeSpan KnockdownTime = TimeSpan.FromSeconds(2);
    [DataField] public SoundSpecifier ActivationSound = new SoundCollectionSpecifier("RadiationPulse");
}

[RegisterComponent]
public sealed partial class ModModuleHolsterComponent : Component
{
    [DataField]
    public string ContainerId = "module_weapon";
}

[RegisterComponent]
public sealed partial class ModModuleGrabberToolComponent : Component
{
    [DataField] public int MaxContents = 2;
    [DataField] public float Delay = 3f;
    [DataField] public float UseCost = 10f;
    public EntityUid? Module;
}

[RegisterComponent]
public sealed partial class ModModuleMicrowaveToolComponent : Component
{
    [DataField] public float Delay = 3f;
    [DataField] public float Heat = 1500f;
    [DataField] public float UseCost = 10f;
    public EntityUid? Module;
}

[DataDefinition]
public sealed partial class ModIntegratedItem
{
    [DataField(required: true)]
    public EntProtoId Item;

    [DataField]
    public Hand Hand = new();
}

public enum ModEffectTarget : byte
{
    Controller,
    Wearer,
    Helmet,
    Torso,
    Gloves,
    Boots,
}

public enum ModPart : byte
{
    Helmet,
    Torso,
    Gloves,
    Boots,
}

public sealed partial class ModModuleActionEvent : InstantActionEvent;
public sealed partial class ModModuleToggleLightEvent : InstantActionEvent;
public sealed partial class ModModuleTeleportEvent : InstantActionEvent;
public sealed partial class ModModuleDispenseEvent : InstantActionEvent;
public sealed partial class ModModuleHolsterEvent : InstantActionEvent;
public sealed partial class ModModuleEnergyShieldEvent : InstantActionEvent;
public sealed partial class ModModuleTanningEvent : InstantActionEvent;
public sealed partial class ModModuleAtrocinatorEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class ModModuleGrabDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ModModuleMicrowaveDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ModModuleInstallDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ModModuleRemoveDoAfterEvent : SimpleDoAfterEvent;

[ByRefEvent]
public readonly record struct ModModuleInstalledEvent(EntityUid Controller);

[ByRefEvent]
public readonly record struct ModModuleUninstalledEvent(EntityUid Controller);

[ByRefEvent]
public readonly record struct ModModuleActivatedEvent(EntityUid Controller, EntityUid Wearer);

[ByRefEvent]
public readonly record struct ModModuleDeactivatedEvent(EntityUid Controller, EntityUid? Wearer);

[ByRefEvent]
public readonly record struct ModModuleUsedEvent(EntityUid Controller, EntityUid Wearer, EntityUid Performer);
