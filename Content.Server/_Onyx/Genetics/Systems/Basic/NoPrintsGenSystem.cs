// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Forensics.Components;
using Content.Shared.Genetics;

namespace Content.Server.Genetics.System;

public sealed partial class NoPrintsGenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NoPrintsGenComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NoPrintsGenComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<NoPrintsGenComponent> ent, ref ComponentInit args)
    {
        if (TryComp<FingerprintComponent>(ent, out var fingerprint))
        {
            if (fingerprint.Fingerprint is { } prints)
                ent.Comp.OldPrints = prints;

            RemComp<FingerprintComponent>(ent);
        }
    }

    private void OnShutdown(Entity<NoPrintsGenComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        EnsureComp<FingerprintComponent>(ent).Fingerprint = ent.Comp.OldPrints;
    }
}
