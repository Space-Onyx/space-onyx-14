using System.Text;
using System.Linq;
using Content.Server.Silicons.Laws;
using Content.Shared._Onyx.CustomLawboard;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;

namespace Content.Server._Onyx.CustomLawboard;

public sealed partial class CustomLawboardSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SiliconLawSystem _siliconLaws = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CustomLawboardComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CustomLawboardComponent, CustomLawboardChangeLawsMessage>(OnChangeLaws);
    }

    private void OnMapInit(Entity<CustomLawboardComponent> ent, ref MapInitEvent args)
    {
        SetLaws(ent, Normalize(ent.Comp.Laws.Select(law => law.LawString)));
    }

    private void OnChangeLaws(Entity<CustomLawboardComponent> ent, ref CustomLawboardChangeLawsMessage args)
    {
        var laws = Normalize(args.Laws);
        SetLaws(ent, laws);

        _adminLogger.Add(
            LogType.SiliconLaw,
            LogImpact.High,
            $"{ToPrettyString(args.Actor)} changed laws on {ToPrettyString(ent)} to [{string.Join(" / ", laws.Select(law => $"{law.Order}: {law.LawString}"))}]");
        _popup.PopupEntity(Loc.GetString("custom-lawboard-updated"), ent, args.Actor);
    }

    private void SetLaws(Entity<CustomLawboardComponent> ent, List<SiliconLaw> laws)
    {
        var provider = EnsureComp<SiliconLawProviderComponent>(ent);
        var fallback = _siliconLaws.GetLawset(provider.Laws);

        ent.Comp.Laws = laws;
        _siliconLaws.SetProviderLawset(
            (ent.Owner, provider),
            new SiliconLawset
            {
                Laws = laws.Select(law => law.ShallowClone()).ToList(),
                ObeysTo = fallback.ObeysTo
            });
        Dirty(ent);
    }

    private static List<SiliconLaw> Normalize(IEnumerable<string> input)
    {
        var result = new List<SiliconLaw>(CustomLawboardComponent.MaxLaws);
        foreach (var raw in input)
        {
            if (result.Count >= CustomLawboardComponent.MaxLaws)
                break;

            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var builder = new StringBuilder(Math.Min(raw.Length, CustomLawboardComponent.MaxLawLength));
            foreach (var rune in raw.EnumerateRunes())
            {
                if (Rune.IsControl(rune))
                {
                    if (builder.Length > 0 &&
                        builder.Length < CustomLawboardComponent.MaxLawLength &&
                        builder[^1] != ' ')
                    {
                        builder.Append(' ');
                    }
                    continue;
                }

                if (builder.Length + rune.Utf16SequenceLength > CustomLawboardComponent.MaxLawLength)
                    break;

                builder.Append(rune);
            }

            var text = builder.ToString().Trim();
            if (text.Length == 0)
                continue;

            result.Add(new SiliconLaw
            {
                LawString = text,
                Order = FixedPoint2.New(result.Count + 1)
            });
        }

        return result;
    }
}
