// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Language;
using Content.Shared.Chat;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.TTS;

public sealed partial class TTSSystem
{
    private bool TryResolveLanguage(EntitySpokeEvent args, out LanguagePrototype language)
    {
        return EntityManager.System<Content.Server._Onyx.Language.LanguageSystem>()
            .TryResolveLanguage(args.Source, args.Language, out language);
    }

    private void FilterLanguageReceivers(ProtoId<LanguagePrototype> language, Filter filter, bool understood)
    {
        var languages = EntityManager.System<Content.Server._Onyx.Language.LanguageSystem>();
        filter.RemoveWhere(session => session.AttachedEntity is not { } listener ||
            languages.CanUnderstand(listener, language) != understood);
    }
}
