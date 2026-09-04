using Content.Shared.Body;
using Content.Shared.Corvax.TTS;
using Content.Shared._Onyx.SpeechBarks;
using Content.Shared.DetailExaminable;
using Content.Shared.Forensics.Components;
using Content.Shared.Genetics;
using Content.Shared.Inventory;

namespace Content.Server.Genetics.System;

public sealed partial class DnaModifierSystem
{
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public bool TryCloneHumanoid(Entity<DnaModifierComponent> entity, Entity<DnaModifierComponent> target)
    {
        if (target.Comp.UniqueIdentifiers == null)
            return false;

        CloneHumanoid(entity, target);

        return true;
    }

    private void CloneHumanoid(Entity<DnaModifierComponent> entity, Entity<DnaModifierComponent> target,
        VisualBodyComponent? visualBody = null, VisualBodyComponent? targetVisualBody = null)
    {
        if (!Resolve(entity, ref visualBody) || !Resolve(target, ref targetVisualBody))
            return;

        if (target.Comp.UniqueIdentifiers == null)
            return;

        EnsureComp<DnaClonedComponent>(entity);

        // Clone all markings
        if (_visualBody.TryGatherMarkingsData(target.Owner, null, out _, out _, out var targetApplied))
            _visualBody.ApplyMarkings(entity, targetApplied);

        entity.Comp.UniqueIdentifiers = CloneUniqueIdentifiers(target.Comp.UniqueIdentifiers);

        if (TryComp<DetailExaminableComponent>(entity, out var detail) &&
            TryComp<DetailExaminableComponent>(target, out var targetDetail))
            detail.Content = targetDetail.Content;

        _metaData.SetEntityName(entity, Name(target));
        if (TryComp<DnaComponent>(entity, out var dna) && TryComp<DnaComponent>(target, out var targetDna))
            dna.DNA = targetDna.DNA;

        if (TryComp<TTSComponent>(entity, out var tts) && TryComp<TTSComponent>(target, out var targetTts))
            tts.VoicePrototypeId = targetTts.VoicePrototypeId;

        if (TryComp<SpeechBarksComponent>(entity, out var barks) && TryComp<SpeechBarksComponent>(target, out var targetBarks))
        {
            barks.Data = targetBarks.Data.Copy();
            Dirty(entity, barks);
        }

        if (TryComp<InventoryComponent>(entity, out var inventory) && TryComp<InventoryComponent>(target, out var targetInventory))
        {
            _inventory.CloneInventory((entity, inventory), targetInventory);
            Dirty(entity, inventory);
        }

        entity.Comp.UniqueIdentifiers!.Gender = target.Comp.UniqueIdentifiers!.Gender;

        Dirty(entity, entity.Comp);
        TryChangeUniqueIdentifiers(entity);
    }
}
