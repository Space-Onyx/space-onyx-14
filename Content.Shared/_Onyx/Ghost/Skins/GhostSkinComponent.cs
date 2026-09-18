// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Ghost.Skins;

/// <summary>
/// Marks an observer as wearing a ghost skin.
/// The client applies the skin prototype visuals on top of whatever ghost entity this is,
/// so skins work on regular observers, admin ghosts and any future observer alike.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GhostSkinComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<GhostSkinPrototype> Skin = GhostSkinPrototype.DefaultSkinId;
}
