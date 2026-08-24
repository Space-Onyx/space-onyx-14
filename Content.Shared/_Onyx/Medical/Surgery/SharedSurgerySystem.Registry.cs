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
            if (prototype.Abstract)
                continue;

            var id = new EntProtoId(prototype.ID);
            if (prototype.TryComp(out SurgeryComponent? surgery, _compFactory))
                _surgeryPrototypes[id] = surgery;
            if (prototype.HasComp<SurgeryStepComponent>(_compFactory))
                _stepPrototypes.Add(id);
        }

        foreach (var (id, surgery) in _surgeryPrototypes.ToArray())
        {
            if (!ValidateSurgeryPrototype(id, surgery))
                _surgeryPrototypes.Remove(id);
        }

        RemoveSurgeriesWithInvalidReferences();
        RemoveSurgeryCycles();
        RemoveSurgeriesWithInvalidReferences();
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
            if (!_stepPrototypes.Contains(stepId) && !_surgeryPrototypes.ContainsKey(stepId))
            {
                Log.Error($"Surgery prototype {id} references invalid step or surgery {stepId} and will be ignored.");
                return false;
            }
        }

        return true;
    }

    private void RemoveSurgeriesWithInvalidReferences()
    {
        while (true)
        {
            var invalid = _surgeryPrototypes
                .Where(entry => entry.Value.Steps.Values.SelectMany(sequence => sequence.Steps)
                    .Any(id => !_stepPrototypes.Contains(id) && !_surgeryPrototypes.ContainsKey(id)))
                .Select(entry => entry.Key)
                .ToArray();

            if (invalid.Length == 0)
                return;

            foreach (var surgery in invalid)
            {
                Log.Error($"Surgery prototype {surgery} contains an invalid nested surgery and will be ignored.");
                _surgeryPrototypes.Remove(surgery);
            }
        }
    }

    private void RemoveSurgeryCycles()
    {
        var visited = new HashSet<EntProtoId>();
        var visiting = new HashSet<EntProtoId>();
        var stack = new List<EntProtoId>();
        var cyclic = new HashSet<EntProtoId>();

        foreach (var surgery in _surgeryPrototypes.Keys)
            VisitSurgery(surgery, visited, visiting, stack, cyclic);

        foreach (var surgery in cyclic)
        {
            Log.Error($"Surgery prototype {surgery} has a nested surgery cycle and will be ignored.");
            _surgeryPrototypes.Remove(surgery);
        }
    }

    private void VisitSurgery(
        EntProtoId surgery,
        HashSet<EntProtoId> visited,
        HashSet<EntProtoId> visiting,
        List<EntProtoId> stack,
        HashSet<EntProtoId> cyclic)
    {
        if (visited.Contains(surgery))
            return;

        if (!visiting.Add(surgery))
        {
            cyclic.UnionWith(stack.SkipWhile(id => id != surgery));
            return;
        }

        stack.Add(surgery);
        foreach (var nested in _surgeryPrototypes[surgery].Steps.Values
                     .SelectMany(sequence => sequence.Steps)
                     .Where(_surgeryPrototypes.ContainsKey))
            VisitSurgery(nested, visited, visiting, stack, cyclic);

        stack.RemoveAt(stack.Count - 1);
        visiting.Remove(surgery);
        visited.Add(surgery);
    }
}
