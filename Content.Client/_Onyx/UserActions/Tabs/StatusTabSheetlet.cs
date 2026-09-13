// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Onyx.UserActions.Tabs;

[CommonSheetlet]
public sealed class StatusTabSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        return
        [
            E<Label>().Class("StatusTabFont").Font(sheet.BaseFont.GetFont(11)),
            E<Label>().Class(StyleClass.LabelKeyText, "StatusTabFont").Font(sheet.BaseFont.GetFont(11, FontKind.Bold)),
        ];
    }
}
