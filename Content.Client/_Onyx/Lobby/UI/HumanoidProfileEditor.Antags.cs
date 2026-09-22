// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void AddAntagCard(BoxContainer antagContainer)
    {
        AntagList.AddChild(new PanelContainer
        {
            StyleClasses = { "PersonalizationCard" },
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalExpand = true,
            Children = { new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(8, 6),
                HorizontalExpand = true,
                Children = { antagContainer },
            } },
        });
    }
}
