using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.AlternativeJobs;

[Prototype]
public sealed partial class AlternativeJobPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string JobName { get; private set; } = default!;

    public string LocalizedJobName => Loc.GetString(JobName);

    [DataField]
    public ProtoId<JobIconPrototype>? JobIconProtoId { get; private set; }

    [DataField(required: true)]
    public ProtoId<JobPrototype> ParentJobId { get; private set; } = default!;
}
