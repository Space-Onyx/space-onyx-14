using System.Linq;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.HealthExaminable;

public sealed partial class HealthExaminableSystem
{
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private void AddPartStatusMarkup(EntityUid examined, EntityUid examiner, FormattedMessage message)
    {
        var parts = _body.GetBodyChildren(examined)
            .OrderBy(part => PartOrder(part.Component.PartType))
            .ThenBy(part => part.Component.Symmetry)
            .ToList();
        if (parts.Count == 0)
            return;

        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString(examined == examiner
                ? "health-examinable-part-title-self"
                : "health-examinable-part-title-other",
            ("entity", Identity.Name(examined, EntityManager))));
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("health-examinable-part-border"));

        foreach (var (part, _) in parts)
        {
            var details = new List<string>();
            var totalDamage = 0f;
            if (TryComp(part, out DamageableComponent? damageable))
            {
                foreach (var (type, amount) in _damageable.GetPositiveDamage((part, damageable)).DamageDict
                             .OrderBy(entry => entry.Key.Id))
                {
                    if (!_prototypes.TryIndex<DamageTypePrototype>(type, out var damageType))
                        continue;

                    totalDamage += amount.Float();
                    details.Add(Loc.GetString("health-examinable-part-damage",
                        ("type", damageType.LocalizedName)));
                }
            }

            var woundStates = new Dictionary<WoundState, int>();
            var bleeding = false;
            var fracture = FractureGrade.None;
            var scars = 0;
            foreach (var wound in _wounds.GetWounds(part))
            {
                if (HasComp<WoundScarComponent>(wound))
                    scars++;

                if (!_prototypes.TryIndex(wound.Comp.Prototype, out var prototype) ||
                    prototype.Visibility != WoundVisibility.Visible)
                    continue;

                if (wound.Comp.State is WoundState.Healed or WoundState.Scarred)
                    continue;

                woundStates[wound.Comp.State] = woundStates.GetValueOrDefault(wound.Comp.State) + 1;
                bleeding |= CompOrNull<WoundBleedingComponent>(wound)?.CurrentRate > 0f;
                if (CompOrNull<WoundFractureComponent>(wound) is { } found && found.Grade > fracture)
                    fracture = found.Grade;
            }

            foreach (var state in new[] { WoundState.Open, WoundState.Stabilized, WoundState.Closed })
            {
                if (woundStates.TryGetValue(state, out var count))
                    details.Add(Loc.GetString($"health-examinable-part-wound-{state.ToString().ToLowerInvariant()}",
                        ("count", count)));
            }

            if (bleeding)
                details.Add(Loc.GetString("health-examinable-part-bleeding"));
            if (fracture != FractureGrade.None)
                details.Add(Loc.GetString($"health-examinable-part-fracture-{fracture.ToString().ToLowerInvariant()}"));
            if (scars > 0)
                details.Add(Loc.GetString("health-examinable-part-scars", ("count", scars)));

            var severity = PartStatusSystem.GetSeverity(totalDamage).ToString().ToLowerInvariant();
            var painLevel = examined == examiner ? GetPainLevel(part) : null;
            var summary = Loc.GetString(painLevel == null
                    ? "health-examinable-part-summary"
                    : "health-examinable-part-summary-pain",
                ("part", Name(part)),
                ("severity", Loc.GetString($"health-examinable-part-severity-{severity}")),
                ("pain", painLevel == null ? string.Empty : Loc.GetString($"health-examinable-pain-{painLevel}")));
            var detail = details.Count == 0 ? string.Empty : string.Join(", ", details);

            message.PushNewline();
            message.AddMarkupOrThrow($"[partstatus summary=\"{FormattedMessage.EscapeStringParameter(summary)}\" details=\"{FormattedMessage.EscapeStringParameter(detail)}\" /]");
            message.AddMarkupOrThrow(Loc.GetString(detail.Length == 0
                    ? "health-examinable-part-chat-line"
                    : "health-examinable-part-chat-line-details",
                ("summary", summary),
                ("details", detail)));
            message.AddMarkupOrThrow("[partstatusend /]");
        }
    }

    private static int PartOrder(BodyPartType type) => type switch
    {
        BodyPartType.Head => 0,
        BodyPartType.Torso or BodyPartType.Chest => 1,
        BodyPartType.Groin => 2,
        BodyPartType.Arm => 3,
        BodyPartType.Hand => 4,
        BodyPartType.Leg => 5,
        BodyPartType.Foot => 6,
        BodyPartType.Tail => 7,
        _ => 8,
    };
}
