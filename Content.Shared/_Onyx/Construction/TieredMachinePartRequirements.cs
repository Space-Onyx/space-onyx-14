// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Construction.Components;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Construction;

public static class TieredMachinePartRequirements
{
    public static readonly ProtoId<StackPrototype> LegacyManipulator = "Manipulator";

    public static void CopyFromBoard(MachineBoardComponent board, Dictionary<MachinePartKind, int> destination)
    {
        destination.Clear();
        foreach (var (kind, amount) in board.PartRequirements)
            destination[kind] = amount;

        if (destination.Count == 0 && board.StackRequirements.TryGetValue(LegacyManipulator, out var manipulators))
            destination[MachinePartKind.Servo] = manipulators;
    }

    public static bool ReplacesManipulators(MachineBoardComponent board)
    {
        return board.PartRequirements.Count > 0 || board.StackRequirements.ContainsKey(LegacyManipulator);
    }
}
