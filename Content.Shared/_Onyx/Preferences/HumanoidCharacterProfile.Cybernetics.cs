using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    private List<EntProtoId> _cybernetics = [];

    public IReadOnlyList<EntProtoId> Cybernetics => _cybernetics;

    public HumanoidCharacterProfile WithCybernetics(IEnumerable<EntProtoId> cybernetics)
    {
        return new(this)
        {
            _cybernetics = cybernetics.Distinct().ToList(),
        };
    }
}
