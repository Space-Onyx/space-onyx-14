// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Client.UserInterface.RichText;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

/// <summary>
///     Marker tag for examine-style notices. It does not render anything itself;
///     <see cref="ChatOutputPanel"/> looks for it and paints a background box behind the message.
/// </summary>
public sealed class ExamineBorderTag : IMarkupTagHandler
{
    public const string TagName = "examineborder";

    public string Name => TagName;
}
