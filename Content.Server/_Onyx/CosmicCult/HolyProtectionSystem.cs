using Content.Server.Bible.Components;
using Content.Server.Popups;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Onyx.CosmicCult;

/// <summary>
/// Target-native holy immunity contract for cosmic cult abilities.
/// </summary>
public sealed partial class HolyProtectionSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier DenialSound = new SoundPathSpecifier("/Audio/Effects/hallelujah.ogg");

    public bool ShouldDeny(EntityUid target)
    {
        foreach (var held in _hands.EnumerateHeld(target))
        {
            if (HasComp<BibleComponent>(held))
                return true;
        }

        return _inventory.TryGetSlotEntity(target, "belt", out var belt) &&
            HasComp<BibleComponent>(belt);
    }

    public bool TouchSpellDenied(EntityUid target)
    {
        if (!ShouldDeny(target))
            return false;

        _popup.PopupEntity(Loc.GetString("cosmic-holy-spell-denied"), target, target, PopupType.MediumCaution);
        _audio.PlayPvs(DenialSound, target);
        Spawn("EffectSparks", Transform(target).Coordinates);
        return true;
    }
}
