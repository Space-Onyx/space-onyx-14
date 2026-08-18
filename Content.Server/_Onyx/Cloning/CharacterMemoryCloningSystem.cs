using System.Linq;
using Content.Shared.Cloning.Events;
using Content.Shared.Mind;

namespace Content.Server._Onyx.Cloning;

public sealed class CharacterMemoryCloningSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CharacterMemoryComponent, CloningEvent>(OnCloneCharacterMemory);
    }

    private void OnCloneCharacterMemory(Entity<CharacterMemoryComponent> original, ref CloningEvent args)
    {
        var cloneMemory = EnsureComp<CharacterMemoryComponent>(args.CloneUid);
        cloneMemory.Memories = original.Comp.Memories
            .Select(memory => new Memory(memory.Name, memory.Value, memory.EntityId))
            .ToHashSet();

        Dirty(args.CloneUid, cloneMemory);
    }
}
