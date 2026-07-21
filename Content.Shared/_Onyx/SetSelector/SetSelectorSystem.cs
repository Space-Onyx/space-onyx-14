// SPDX-FileCopyrightText: 2025 Conchelle <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 IrisTheAmped <iristheamped@gmail.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 Ted Lukin <66275205+pheenty@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.EntityTable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.SetSelector;

public sealed partial class SetSelectorSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SetSelectorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SetSelectorComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<SetSelectorComponent, SetSelectorApproveMessage>(OnApprove);
        SubscribeLocalEvent<SetSelectorComponent, SetSelectorChangeSetMessage>(OnChangeSet);
    }

    private void OnMapInit(Entity<SetSelectorComponent> selector, ref MapInitEvent args)
    {
        if (selector.Comp.SetsToSelect == -1)
        {
            selector.Comp.AvailableSets = selector.Comp.PossibleSets;
            return;
        }

        var sets = selector.Comp.PossibleSets.ToList();
        _random.Shuffle(sets);
        selector.Comp.AvailableSets = sets.Take(selector.Comp.SetsToSelect).ToList();
    }

    private void OnUiOpened(Entity<SetSelectorComponent> selector, ref BoundUIOpenedEvent args)
    {
        UpdateUi(selector);
    }

    private void OnApprove(Entity<SetSelectorComponent> selector, ref SetSelectorApproveMessage args)
    {
        if (selector.Comp.SelectedSets.Count != selector.Comp.MaxSelectedSets)
            return;

        var coordinates = _transform.GetMapCoordinates(selector);
        _container.TryGetContainingContainer(selector.Owner, out var target);
        var ignoredContainers = new List<string> { "implant", "pocket1", "pocket2", "pocket3", "pocket4" };
        var spawnedEntities = new List<EntityUid>();

        foreach (var setIndex in selector.Comp.SelectedSets)
        {
            var set = _proto.Index(selector.Comp.AvailableSets[setIndex]);
            spawnedEntities.AddRange(set.Content.Select(item => Spawn(item, coordinates)));

            foreach (var tableId in set.Tables)
            {
                var table = _proto.Index(tableId);
                spawnedEntities.AddRange(_entityTable.GetSpawns(table).Select(spawn => Spawn(spawn, coordinates)));
            }
        }

        _audio.PlayPvs(selector.Comp.ApproveSound, Transform(selector).Coordinates);

        var storagePrototype = selector.Comp.SpawnedStoragePrototype;
        var storageContainer = selector.Comp.SpawnedStorageContainer;
        var openStorage = selector.Comp.OpenSpawnedStorage;
        Del(selector);

        EntityUid? spawnedStorage = null;
        if (storagePrototype != null && storageContainer != null)
        {
            spawnedStorage = Spawn(storagePrototype, coordinates);
            RecursiveInsert(spawnedStorage.Value, target, ignoredContainers);
            _container.TryGetContainer(spawnedStorage.Value, storageContainer, out target);
        }

        ignoredContainers.AddRange(_hands.EnumerateHands(args.Actor));
        foreach (var entity in spawnedEntities)
        {
            RecursiveInsert(entity, target, ignoredContainers);
        }

        if (openStorage && spawnedStorage != null)
            _entityStorage.OpenStorage(spawnedStorage.Value, args.Actor);
    }

    private bool RecursiveInsert(EntityUid entity, BaseContainer? container, List<string> ignoredContainers)
    {
        if (container == null)
            return false;

        if (!ignoredContainers.Contains(container.ID) && _container.Insert((entity, null, null, null), container))
            return true;

        if (Transform(container.Owner).ParentUid.IsValid() &&
            _container.TryGetContainingContainer(container.Owner, out var parentContainer))
        {
            return RecursiveInsert(entity, parentContainer, ignoredContainers);
        }

        return false;
    }

    private void OnChangeSet(Entity<SetSelectorComponent> selector, ref SetSelectorChangeSetMessage args)
    {
        if (args.SetNumber < 0 || args.SetNumber >= selector.Comp.AvailableSets.Count)
            return;

        if (!selector.Comp.SelectedSets.Remove(args.SetNumber))
        {
            if (selector.Comp.SelectedSets.Count >= selector.Comp.MaxSelectedSets)
                return;

            selector.Comp.SelectedSets.Add(args.SetNumber);
        }

        UpdateUi(selector);
    }

    private void UpdateUi(Entity<SetSelectorComponent> selector)
    {
        var data = new Dictionary<int, SelectableSetInfo>();
        for (var i = 0; i < selector.Comp.AvailableSets.Count; i++)
        {
            var set = _proto.Index(selector.Comp.AvailableSets[i]);
            data.Add(i, new SelectableSetInfo(
                set.Name,
                set.Description,
                set.Sprite,
                selector.Comp.SelectedSets.Contains(i)));
        }

        _ui.SetUiState(selector.Owner,
            SetSelectorUIKey.Key,
            new SetSelectorBoundUserInterfaceState(data, selector.Comp.MaxSelectedSets));
    }
}
