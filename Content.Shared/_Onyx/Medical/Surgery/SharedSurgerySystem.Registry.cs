using System.Linq;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private readonly Dictionary<EntProtoId, EntityUid> _singletons = new();
    private readonly Dictionary<EntProtoId, SurgeryComponent> _surgeryPrototypes = new();
    private readonly HashSet<EntProtoId> _stepPrototypes = new();

    protected IReadOnlyCollection<EntProtoId> SurgeryPrototypes => _surgeryPrototypes.Keys;

    protected EntityUid? GetSurgeryEntity(EntProtoId surgery)
    {
        return _surgeryPrototypes.ContainsKey(surgery) ? GetOrSpawnPrototypeEntity(surgery) : null;
    }

    public EntityUid? GetSurgeryStepEntity(EntProtoId step)
    {
        return _stepPrototypes.Contains(step) ? GetOrSpawnPrototypeEntity(step) : null;
    }

    private EntityUid GetOrSpawnPrototypeEntity(EntProtoId prototype)
    {
        if (!_singletons.TryGetValue(prototype, out var ent) || TerminatingOrDeleted(ent))
        {
            ent = Spawn(prototype, MapCoordinates.Nullspace);
            _singletons[prototype] = ent;
        }

        return ent;
    }

    private void LoadSurgeryPrototypes()
    {
        _surgeryPrototypes.Clear();
        _stepPrototypes.Clear();

        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.Abstract ||
                !prototype.TryComp(out SurgeryComponent? surgery, _compFactory))
                continue;

            _surgeryPrototypes[new EntProtoId(prototype.ID)] = surgery;
        }

        foreach (var (id, surgery) in _surgeryPrototypes.ToArray())
        {
            if (!ValidateSurgeryPrototype(id, surgery))
                _surgeryPrototypes.Remove(id);
        }

        RemoveSurgeriesWithInvalidRequirements();
        RemoveRequirementCycles();
        RemoveSurgeriesWithInvalidRequirements();

        foreach (var surgery in _surgeryPrototypes.Values)
            foreach (var sequence in surgery.Steps.Values)
                _stepPrototypes.UnionWith(sequence.Steps);
    }

    private bool ValidateSurgeryPrototype(EntProtoId id, SurgeryComponent surgery)
    {
        if (surgery.Steps.Count == 0)
        {
            Log.Error($"Surgery prototype {id} has no steps and will be ignored.");
            return false;
        }

        if (surgery.Steps.Values.Count(sequence => sequence.Required.Count == 0) != 1)
        {
            Log.Error($"Surgery prototype {id} must have exactly one fallback step section and will be ignored.");
            return false;
        }

        if (surgery.Steps.Any(entry => entry.Value.Steps.Count == 0))
        {
            Log.Error($"Surgery prototype {id} contains an empty step section and will be ignored.");
            return false;
        }

        foreach (var stepId in surgery.Steps.Values.SelectMany(sequence => sequence.Steps))
        {
            if (!_prototypes.TryIndex<EntityPrototype>(stepId, out var stepPrototype) ||
                stepPrototype.Abstract ||
                !stepPrototype.HasComp<SurgeryStepComponent>(_compFactory))
            {
                Log.Error($"Surgery prototype {id} references invalid step {stepId} and will be ignored.");
                return false;
            }
        }

        if (surgery.Requirement is { } requirement && !_surgeryPrototypes.ContainsKey(requirement))
        {
            Log.Error($"Surgery prototype {id} references invalid requirement {requirement} and will be ignored.");
            return false;
        }

        return true;
    }

    private void RemoveSurgeriesWithInvalidRequirements()
    {
        while (true)
        {
            var invalid = _surgeryPrototypes
                .Where(entry => entry.Value.Requirement is { } requirement &&
                    !_surgeryPrototypes.ContainsKey(requirement))
                .Select(entry => entry.Key)
                .ToArray();

            if (invalid.Length == 0)
                return;

            foreach (var surgery in invalid)
            {
                Log.Error($"Surgery prototype {surgery} depends on an invalid requirement and will be ignored.");
                _surgeryPrototypes.Remove(surgery);
            }
        }
    }

    private void RemoveRequirementCycles()
    {
        var visited = new HashSet<EntProtoId>();
        var visiting = new HashSet<EntProtoId>();
        var cyclic = new HashSet<EntProtoId>();

        foreach (var surgery in _surgeryPrototypes.Keys)
            VisitRequirement(surgery, visited, visiting, cyclic);

        foreach (var surgery in cyclic)
        {
            Log.Error($"Surgery prototype {surgery} has a requirement cycle and will be ignored.");
            _surgeryPrototypes.Remove(surgery);
        }
    }

    private void VisitRequirement(
        EntProtoId surgery,
        HashSet<EntProtoId> visited,
        HashSet<EntProtoId> visiting,
        HashSet<EntProtoId> cyclic)
    {
        if (visited.Contains(surgery))
            return;

        if (!visiting.Add(surgery))
        {
            cyclic.UnionWith(visiting);
            return;
        }

        if (_surgeryPrototypes[surgery].Requirement is { } requirement)
            VisitRequirement(requirement, visited, visiting, cyclic);

        visiting.Remove(surgery);
        visited.Add(surgery);
    }
}
