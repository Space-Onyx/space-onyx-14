// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Onyx.Lobby.UI;

[CommonSheetlet]
public sealed class PersonalizationSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var header = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundLight)
        {
            BorderColor = sheet.SecondaryPalette.Element,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        var section = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundDark)
        {
            BorderColor = sheet.SecondaryPalette.Element,
            BorderThickness = new Thickness(1),
        };
        var sectionHeader = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundLight)
        {
            BorderColor = sheet.HighlightPalette.Element,
            BorderThickness = new Thickness(3, 0, 0, 0),
        };
        var lobbyPanel = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundDark.WithAlpha(0.9f))
        {
            BorderColor = sheet.SecondaryPalette.Element,
            BorderThickness = new Thickness(1),
        };
        var lobbyHeader = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundLight.WithAlpha(0.94f))
        {
            BorderColor = sheet.SecondaryPalette.Element,
            BorderThickness = new Thickness(1),
        };
        var lobbyInset = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundDark.WithAlpha(0.96f))
        {
            BorderColor = sheet.SecondaryPalette.Element,
            BorderThickness = new Thickness(1),
        };

        return
        [
            E<PanelContainer>().Class("AppearancePersonalizationHeader").Panel(header),
            E<PanelContainer>().Class("AppearancePersonalizationSection").Panel(section),
            E<PanelContainer>().Class("AppearancePersonalizationSectionHeader").Panel(sectionHeader),
            E<Label>().Class("AppearancePersonalizationTitle").Font(sheet.BaseFont.GetFont(12)),
            E<PanelContainer>().Class("PersonalizationHeader").Panel(header),
            E<PanelContainer>().Class("PersonalizationTools").Panel(section),
            E<PanelContainer>().Class("PersonalizationCard").Panel(section),
            E<Label>().Class("PersonalizationTitle").Font(sheet.BaseFont.GetFont(12)),
            E<PanelContainer>().Class("LobbyPanel").Panel(lobbyPanel),
            E<PanelContainer>().Class("LobbyHeader").Panel(lobbyHeader),
            E<PanelContainer>().Class("LobbyInset").Panel(lobbyInset),
        ];
    }
}
