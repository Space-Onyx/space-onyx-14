using System.Linq;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;

namespace Content.Server.Silicons.Laws;

public sealed partial class SiliconLawSystem
{
    public SiliconLawset CopyLawset(
        Entity<SiliconLawProviderComponent> source,
        Entity<SiliconLawProviderComponent> target)
    {
        var sourceLawset = source.Comp.Lawset ?? GetLawset(source.Comp.Laws);
        var copy = new SiliconLawset
        {
            Laws = sourceLawset.Laws.Select(law => law.ShallowClone()).ToList(),
            ObeysTo = sourceLawset.ObeysTo
        };

        target.Comp.Lawset = copy;
        RankLaws(copy.Laws);
        NotifyLawsChanged(target, source.Comp.LawUploadSound);
        Dirty(target);
        return copy;
    }
}
