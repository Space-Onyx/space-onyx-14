using System.Linq;
using Content.Shared._Onyx.AlternativeJobs;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.AlternativeJobs;

public sealed partial class AlternativeJobSelector : OptionButton
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private readonly ProtoId<JobPrototype> _parentJobId;
    private readonly List<AlternativeJobPrototype> _alternatives = [];

    public event Action<ProtoId<AlternativeJobPrototype>?>? OnAlternativeSelected;

    public AlternativeJobSelector(ProtoId<JobPrototype> parentJobId)
    {
        IoCManager.InjectDependencies(this);
        _parentJobId = parentJobId;

        if (_prototypeManager.TryIndex(parentJobId, out var job))
            AddItem(job.LocalizedName, 0);
        else
            AddItem(parentJobId, 0);

        _alternatives.AddRange(_prototypeManager.EnumeratePrototypes<AlternativeJobPrototype>()
            .Where(alternative => alternative.ParentJobId == parentJobId)
            .OrderBy(alternative => alternative.LocalizedJobName));

        for (var i = 0; i < _alternatives.Count; i++)
            AddItem(_alternatives[i].LocalizedJobName, i + 1);

        Visible = _alternatives.Count > 0;
        OnItemSelected += args =>
        {
            SelectId(args.Id);
            OnAlternativeSelected?.Invoke(args.Id == 0 ? null : _alternatives[args.Id - 1].ID);
        };
    }

    public void SelectAlternative(ProtoId<AlternativeJobPrototype>? alternativeId)
    {
        var index = alternativeId is null ? -1 : _alternatives.FindIndex(alternative => alternative.ID == alternativeId);
        SelectId(index + 1);
    }
}
