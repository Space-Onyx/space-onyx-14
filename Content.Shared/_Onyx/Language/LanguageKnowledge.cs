// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Language;

[Flags, Serializable, NetSerializable]
public enum LanguageAbility : byte
{
    None = 0,
    Speak = 1,
    Understand = 2,
    Read = 4,
    Write = 8,
    All = Speak | Understand | Read | Write,
}

/// <summary>One composable knowledge set. Each source contributes independent abilities.</summary>
public static class LanguageKnowledge
{
    /// <summary>Monotone closure; no device is consumed before all its modalities can unlock.</summary>
    public static void ResolveTranslators(IReadOnlyList<BaseTranslatorComponent> translators, CollectLanguageKnowledgeEvent knowledge)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var translator in translators)
            {
                if (!translator.Enabled)
                    continue;
                foreach (var (language, abilities) in translator.Languages)
                {
                    for (var bit = 1; bit <= (int) LanguageAbility.Write; bit <<= 1)
                    {
                        var ability = (LanguageAbility) bit;
                        if (!translator.TranslateWriting && (ability & (LanguageAbility.Read | LanguageAbility.Write)) != 0)
                            continue;
                        if ((abilities & ability) != 0 && RequirementsMet(translator, knowledge, ability))
                            changed |= Grant(knowledge.Languages, language, ability);
                    }
                }
            }
        }
        while (changed);
    }

    private static bool RequirementsMet(BaseTranslatorComponent translator, CollectLanguageKnowledgeEvent knowledge, LanguageAbility ability)
    {
        if ((knowledge.UniversalAbilities & ability) != 0)
            return true;
        foreach (var requirement in translator.Requires)
        {
            if (Has(knowledge.Languages, requirement, ability) != translator.RequiresAll)
                return !translator.RequiresAll;
        }
        return translator.RequiresAll || translator.Requires.Count == 0;
    }

    public static bool Has(
        IReadOnlyDictionary<ProtoId<LanguagePrototype>, LanguageAbility> knowledge,
        ProtoId<LanguagePrototype> language,
        LanguageAbility ability)
    {
        return knowledge.TryGetValue(language, out var available) && (available & ability) == ability;
    }

    public static bool Grant(
        Dictionary<ProtoId<LanguagePrototype>, LanguageAbility> knowledge,
        ProtoId<LanguagePrototype> language,
        LanguageAbility ability)
    {
        knowledge.TryGetValue(language, out var current);
        var combined = current | ability;
        if (combined == current)
            return false;
        knowledge[language] = combined;
        return true;
    }
}
