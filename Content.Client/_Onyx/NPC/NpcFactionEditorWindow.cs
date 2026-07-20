using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.NPC;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using Content.Client.NPC;
using Content.Shared._Onyx.NPC;

namespace Content.Client._Onyx.NPC;

public sealed class NpcFactionEditorWindow : DefaultWindow
{
    private sealed class ColumnUi
    {
        public readonly Label CountLabel;
        public readonly ItemList List;
        public readonly Button SelectVisibleButton;
        public readonly Button ClearVisibleButton;

        public ColumnUi(Label countLabel, ItemList list, Button selectVisibleButton, Button clearVisibleButton)
        {
            CountLabel = countLabel;
            List = list;
            SelectVisibleButton = selectVisibleButton;
            ClearVisibleButton = clearVisibleButton;
        }
    }

    private readonly NpcFactionEditorEui _ui;
    private readonly Label _targetLabel;
    private readonly Label _statusLabel;
    private readonly LineEdit _searchEdit;
    private readonly ColumnUi _factionsColumn;
    private readonly ColumnUi _friendlyOverridesColumn;
    private readonly ColumnUi _hostileOverridesColumn;
    private readonly List<string> _allFactions = new();
    private readonly HashSet<string> _selectedFactions = new();
    private readonly HashSet<string> _selectedFriendlyOverrides = new();
    private readonly HashSet<string> _selectedHostileOverrides = new();

    public NpcFactionEditorWindow(NpcFactionEditorEui ui)
    {
        _ui = ui;
        Title = Loc.GetString("npc-faction-editor-title");
        MinSize = new Vector2(880, 620);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            SeparationOverride = 6
        };
        Contents.AddChild(root);

        _targetLabel = new Label { Text = Loc.GetString("npc-faction-editor-target-empty") };
        _statusLabel = new Label { Text = Loc.GetString("npc-faction-editor-state-waiting") };
        root.AddChild(_targetLabel);
        root.AddChild(_statusLabel);

        var searchRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true
        };
        searchRow.AddChild(new Label
        {
            Text = Loc.GetString("npc-faction-editor-search-label"),
            VerticalAlignment = VAlignment.Center
        });

        _searchEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("npc-faction-editor-search-placeholder")
        };
        _searchEdit.OnTextChanged += _ => RefreshLists();
        searchRow.AddChild(_searchEdit);
        root.AddChild(searchRow);

        var selectors = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            VerticalExpand = true
        };
        root.AddChild(selectors);

        selectors.AddChild(CreateSelectorColumn("npc-faction-editor-column-factions", out _factionsColumn));
        selectors.AddChild(CreateSelectorColumn("npc-faction-editor-column-friendly", out _friendlyOverridesColumn));
        selectors.AddChild(CreateSelectorColumn("npc-faction-editor-column-hostile", out _hostileOverridesColumn));
        HookColumn(_factionsColumn, _selectedFactions);
        HookColumn(_friendlyOverridesColumn, _selectedFriendlyOverrides);
        HookColumn(_hostileOverridesColumn, _selectedHostileOverrides);

        var applyButton = new Button
        {
            Text = Loc.GetString("npc-faction-editor-apply"),
            HorizontalAlignment = HAlignment.Right
        };
        applyButton.OnPressed += _ => ApplyChanges();
        root.AddChild(applyButton);
    }

    public void SetState(NpcFactionEditorEuiState state)
    {
        Title = Loc.GetString("npc-faction-editor-title-target", ("name", state.EntityName));
        _targetLabel.Text = Loc.GetString("npc-faction-editor-target", ("name", state.EntityName), ("uid", state.Target));
        _statusLabel.Text = Loc.GetString("npc-faction-editor-state-updated");

        _allFactions.Clear();
        _allFactions.AddRange(state.AllFactions);

        _selectedFactions.Clear();
        _selectedFactions.UnionWith(state.Factions);
        _selectedFriendlyOverrides.Clear();
        _selectedFriendlyOverrides.UnionWith(state.FriendlyOverrides);
        _selectedHostileOverrides.Clear();
        _selectedHostileOverrides.UnionWith(state.HostileOverrides);

        RefreshLists();
    }

    private static BoxContainer CreateSelectorColumn(string titleLocKey, out ColumnUi column)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
            VerticalExpand = true
        };

        box.AddChild(new Label
        {
            Text = Loc.GetString(titleLocKey),
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Center
        });

        var countLabel = new Label
        {
            HorizontalExpand = true,
            Text = Loc.GetString("npc-faction-editor-count", ("selected", 0), ("total", 0))
        };
        box.AddChild(countLabel);

        var list = new ItemList
        {
            SelectMode = ItemList.ItemListSelectMode.Multiple,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        box.AddChild(list);

        var buttonsRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalExpand = true
        };

        var selectVisible = new Button
        {
            Text = Loc.GetString("npc-faction-editor-select-visible"),
            HorizontalExpand = true
        };
        var clearVisible = new Button
        {
            Text = Loc.GetString("npc-faction-editor-clear-visible"),
            HorizontalExpand = true
        };
        buttonsRow.AddChild(selectVisible);
        buttonsRow.AddChild(clearVisible);
        box.AddChild(buttonsRow);

        column = new ColumnUi(countLabel, list, selectVisible, clearVisible);
        return box;
    }

    private void ApplyChanges()
    {
        var factions = _selectedFactions.ToList();
        var friendly = _selectedFriendlyOverrides.ToList();
        var hostile = _selectedHostileOverrides.ToList();
        factions.Sort();
        friendly.Sort();
        hostile.Sort();

        _ui.ApplyChanges(factions, friendly, hostile);
        _statusLabel.Text = Loc.GetString("npc-faction-editor-state-sent");
    }

    private void HookColumn(ColumnUi column, HashSet<string> selected)
    {
        column.List.OnItemSelected += args => SetColumnItemSelected(args, selected);
        column.List.OnItemDeselected += args => SetColumnItemDeselected(args, selected);
        column.SelectVisibleButton.OnPressed += _ => SetVisibleSelection(column, selected, true);
        column.ClearVisibleButton.OnPressed += _ => SetVisibleSelection(column, selected, false);
    }

    private static void SetColumnItemSelected(ItemList.ItemListSelectedEventArgs args, HashSet<string> selected)
    {
        var item = args.ItemList[args.ItemIndex];
        if (item.Metadata is not string faction)
            return;

        selected.Add(faction);
    }

    private static void SetColumnItemDeselected(ItemList.ItemListDeselectedEventArgs args, HashSet<string> selected)
    {
        var item = args.ItemList[args.ItemIndex];
        if (item.Metadata is not string faction)
            return;

        selected.Remove(faction);
    }

    private void SetVisibleSelection(ColumnUi column, HashSet<string> selected, bool value)
    {
        for (var i = 0; i < column.List.Count; i++)
        {
            var item = column.List[i];
            if (item.Metadata is not string faction)
                continue;

            item.Selected = value;
            if (value)
                selected.Add(faction);
            else
                selected.Remove(faction);
        }

        UpdateColumnCount(column, selected);
    }

    private void RefreshLists()
    {
        var filter = _searchEdit.Text.Trim().ToLowerInvariant();
        FillList(_factionsColumn, _selectedFactions, filter);
        FillList(_friendlyOverridesColumn, _selectedFriendlyOverrides, filter);
        FillList(_hostileOverridesColumn, _selectedHostileOverrides, filter);
    }

    private void FillList(ColumnUi column, HashSet<string> selected, string lowerFilter)
    {
        column.List.Clear();

        foreach (var faction in _allFactions)
        {
            if (lowerFilter.Length > 0 && !faction.ToLowerInvariant().Contains(lowerFilter))
                continue;

            column.List.Add(new ItemList.Item(column.List)
            {
                Text = faction,
                Metadata = faction,
                Selected = selected.Contains(faction)
            });
        }

        UpdateColumnCount(column, selected);
    }

    private void UpdateColumnCount(ColumnUi column, HashSet<string> selected)
    {
        column.CountLabel.Text = Loc.GetString("npc-faction-editor-count", ("selected", selected.Count), ("total", _allFactions.Count));
    }
}
