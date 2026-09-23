// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Resources;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Onyx.UserActions.Tabs;

[CommonSheetlet]
public sealed class StatusTabSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var metricFont = ResCache.GetFont("/Fonts/RobotoMono/RobotoMono-Bold.ttf", size: 12);

        return
        [
            E<Label>().Class("StatusTabHeader").Font(sheet.BaseFont.GetFont(11, FontKind.Bold)),
            E<Label>().Class("StatusTabMetricLabel").Font(sheet.BaseFont.GetFont(10, FontKind.Bold)),
            E<Label>().Class("StatusTabMetricValue").Font(metricFont),
            E<Label>().Class(StyleClass.LabelKeyText, "StatusTabMetricValue").Font(metricFont),
        ];
    }
}
