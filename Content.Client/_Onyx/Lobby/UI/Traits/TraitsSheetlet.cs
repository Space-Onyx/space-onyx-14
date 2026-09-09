// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Onyx.Lobby.UI.Traits;

[CommonSheetlet]
public sealed class TraitsSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var header = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundLight)
        {
            BorderColor = sheet.SecondaryPalette.Element,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        var categoryHeader = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundLight);
        var categoryContent = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundDark);
        var accent = new StyleBoxFlat(sheet.HighlightPalette.Element);
        var card = new StyleBoxFlat(sheet.SecondaryPalette.Background)
        {
            BorderColor = sheet.SecondaryPalette.Element,
            BorderThickness = new Thickness(1),
        };
        var selectedCard = new StyleBoxFlat(sheet.PrimaryPalette.BackgroundLight)
        {
            BorderColor = sheet.HighlightPalette.Element,
            BorderThickness = new Thickness(1),
        };
        var progressBackground = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundDark)
        {
            BorderColor = sheet.SecondaryPalette.Element,
            BorderThickness = new Thickness(1),
        };
        var progressFull = new StyleBoxFlat(sheet.PositivePalette.Element);
        var progressPartial = new StyleBoxFlat(sheet.HighlightPalette.Element);
        var progressLow = new StyleBoxFlat(sheet.NegativePalette.Element);
        var progressEmpty = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundDark);

        return
        [
            E<PanelContainer>().Class("OnyxTraitsHeader").Panel(header),
            E<PanelContainer>().Class("OnyxTraitsSearch").Panel(categoryContent),
            E<PanelContainer>().Class("OnyxTraitCategoryHeader").Panel(categoryHeader),
            E<PanelContainer>().Class("OnyxTraitCategoryAccent").Panel(accent),
            E<PanelContainer>().Class("OnyxTraitCategoryContent").Panel(categoryContent),
            E<PanelContainer>().Class("OnyxTraitCard").Panel(card),
            E<PanelContainer>().Class("OnyxTraitCard", "OnyxTraitCardSelected").Panel(selectedCard),
            E<PanelContainer>().Class("OnyxTraitsProgressBackground").Panel(progressBackground),
            E<PanelContainer>().Class("OnyxTraitsProgressFill").Panel(progressFull),
            E<PanelContainer>().Class("OnyxTraitsProgressFull").Panel(progressFull),
            E<PanelContainer>().Class("OnyxTraitsProgressPartial").Panel(progressPartial),
            E<PanelContainer>().Class("OnyxTraitsProgressLow").Panel(progressLow),
            E<PanelContainer>().Class("OnyxTraitsProgressEmpty").Panel(progressEmpty),
            E<Label>().Class("OnyxTraitTitle").Font(sheet.BaseFont.GetFont(12)),
            E<Label>().Class("OnyxTraitStat").FontColor(sheet.HighlightPalette.Text),
        ];
    }
}
