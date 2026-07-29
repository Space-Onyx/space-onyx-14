// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2025 Rinary <72972221+Rinary1@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 ss14-Starlight <ss14-Starlight@outlook.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.SubFloor;
using Content.Shared._Onyx.VentCrawling;
using Content.Shared.Atmos.Components;
using Robust.Client.Player;

namespace Content.Client._Onyx.VentCrawling;

public sealed partial class VentCrawlingVisionSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SubFloorHideSystem _subFloorHideSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVentTubeSystem _ventTube = default!;

    private readonly HashSet<EntityUid> _visibleTubes = [];
    private static readonly Direction[] Directions = [Direction.North, Direction.South, Direction.East, Direction.West];
    private EntityUid? _selectedHolder;
    private readonly List<int> _pendingLayerSteps = [];
    private uint _layerSelectionSequence;
    private byte _layerCount = 3;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VentCrawlerHolderComponent, VentCrawlerLayerSelectedEvent>(OnLayerSelected);
    }

    private void OnLayerSelected(EntityUid uid, VentCrawlerHolderComponent holder, ref VentCrawlerLayerSelectedEvent args)
    {
        if (args.Layers == 0)
            return;

        if (_selectedHolder != uid)
        {
            _selectedHolder = uid;
            _layerSelectionSequence = holder.LayerSelectionSequence;
            _pendingLayerSteps.Clear();
        }

        _layerCount = args.Layers;
        _pendingLayerSteps.Add(args.Step);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var player = _player.LocalEntity;
        _visibleTubes.Clear();

        if (TryComp<VentCrawlerComponent>(player, out var crawler) && crawler.InTube &&
            TryComp<TransformComponent>(player, out var playerTransform) &&
            TryComp<VentCrawlerHolderComponent>(playerTransform.ParentUid, out var holder) &&
            holder.CurrentTube is { } currentTube &&
            currentTube.IsValid() &&
            TryComp(currentTube, out TransformComponent? _))
        {
            if (_selectedHolder != playerTransform.ParentUid)
            {
                _selectedHolder = playerTransform.ParentUid;
                _layerSelectionSequence = holder.LayerSelectionSequence;
                _pendingLayerSteps.Clear();
            }

            var acknowledged = holder.LayerSelectionSequence - _layerSelectionSequence;
            if (acknowledged > 0)
            {
                var remove = (int) Math.Min(acknowledged, (uint) _pendingLayerSteps.Count);
                _pendingLayerSteps.RemoveRange(0, remove);
                _layerSelectionSequence = holder.LayerSelectionSequence;
            }

            var layer = holder.PipeLayer;
            foreach (var step in _pendingLayerSteps)
                layer = (AtmosPipeLayer) (((int) layer + step + _layerCount) % _layerCount);
            AddConnectedTubes(currentTube, layer);
        }
        else
        {
            _selectedHolder = null;
            _pendingLayerSteps.Clear();
        }

        _subFloorHideSystem.SetVentPipes(_visibleTubes);
    }

    private void AddConnectedTubes(EntityUid currentTube, AtmosPipeLayer layer)
    {
        var origin = _transform.GetWorldPosition(currentTube);
        var pending = new Queue<EntityUid>();
        pending.Enqueue(currentTube);
        _visibleTubes.Add(currentTube);

        while (pending.TryDequeue(out var tube))
        {
            foreach (var direction in Directions)
            {
                if (_ventTube.NextTubeFor(tube, direction, layer) is not { } next ||
                    !next.IsValid() ||
                    _visibleTubes.Contains(next) ||
                    !TryComp(next, out TransformComponent? _))
                    continue;

                var offset = _transform.GetWorldPosition(next) - origin;
                if (MathF.Abs(offset.X) > 1f || MathF.Abs(offset.Y) > 1f)
                    continue;

                _visibleTubes.Add(next);
                pending.Enqueue(next);
            }
        }
    }
}
