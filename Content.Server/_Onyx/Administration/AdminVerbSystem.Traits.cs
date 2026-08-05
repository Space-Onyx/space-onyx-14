using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Content.Shared.Whitelist;

namespace Content.Server.Administration.Systems;

public sealed partial class AdminVerbSystem
{
    [Dependency] private SharedHandsSystem _traitHands = default!;
    [Dependency] private EntityWhitelistSystem _traitWhitelist = default!;

    private void ApplyProfileTraits(EntityUid mob, HumanoidCharacterProfile profile)
    {
        foreach (var traitId in profile.TraitPreferences)
        {
            if (!ProtoMan.TryIndex<TraitPrototype>(traitId, out var trait))
            {
                Log.Error($"No trait found with ID {traitId}!");
                continue;
            }

            if (_traitWhitelist.IsWhitelistFail(trait.Whitelist, mob) ||
                _traitWhitelist.IsWhitelistPass(trait.Blacklist, mob))
                continue;

            if (trait.Components.Count > 0)
                EntityManager.AddComponents(mob, trait.Components, false);

            foreach (var special in trait.Specials)
                special.AfterEquip(mob);

            if (trait.TraitGear == null || !TryComp(mob, out HandsComponent? hands))
                continue;

            var item = Spawn(trait.TraitGear, Transform(mob).Coordinates);
            _traitHands.TryPickup(mob, item, checkActionBlocker: false, handsComp: hands);
        }
    }
}
