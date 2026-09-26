// Content adapted from Goob-Station (https://github.com/Goob-Station/Goob-Station/pull/6891), licensed under AGPL-3.0-or-later.

using Content.Shared.Actions;

namespace Content.Shared._Onyx.Telepathy;

public sealed partial class TelepathyWhisperEvent : EntityTargetActionEvent;

public abstract partial class SharedTelepathySystem : EntitySystem;
