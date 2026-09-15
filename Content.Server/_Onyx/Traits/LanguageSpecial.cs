// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server._Onyx.Language;
using Content.Shared._Onyx.Language;
using Content.Shared._Onyx.Traits;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Traits;

public sealed partial class LanguageSpecial : JobSpecial
{
    [DataField(required: true)]
    public HashSet<ProtoId<LanguagePrototype>> Languages = new();

    public override void AfterEquip(EntityUid mob)
    {
        var entities = IoCManager.Resolve<IEntityManager>();
        var knowledge = entities.EnsureComponent<LanguageTraitComponent>(mob);
        knowledge.Languages.UnionWith(Languages);
        entities.Dirty(mob, knowledge);
        entities.System<LanguageSystem>().UpdateLanguages(mob);
    }
}
