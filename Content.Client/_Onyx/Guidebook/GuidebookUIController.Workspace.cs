// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Numerics;
using Content.Client._Onyx.Guidebook;
using Content.Client.Guidebook.Controls;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;

namespace Content.Client.UserInterface.Systems.Guidebook;

public sealed partial class GuidebookUIController
{
    [Dependency] private IResourceManager _guidebookResources = default!;

    private GuidebookPreferences? _guidebookPreferences;
    private readonly List<GuidebookWindow> _additionalGuideWindows = new();
    private readonly List<OSWindow> _popOutGuideWindows = new();

    private void ConfigureWorkspaceWindow(GuidebookWindow window, bool mainWindow)
    {
        _guidebookPreferences ??= new GuidebookPreferences(_guidebookResources);
        window.ConfigureWorkspace(_guidebookPreferences, mainWindow);
        window.OnNewWindow += OnNewGuidebookWindow;
        window.OnPopOut += OnPopOutGuidebook;
    }

    private void OnNewGuidebookWindow(GuidebookWindow source)
    {
        var window = UIManager.CreateWindow<GuidebookWindow>();
        ConfigureWorkspaceWindow(window, false);
        source.CopyWorkspaceTo(window);
        _additionalGuideWindows.Add(window);
        window.OnClose += () =>
        {
            _additionalGuideWindows.Remove(window);
            window.Dispose();
        };
        window.Open();

        var position = source.Position + new Vector2(32, 32);
        var max = Vector2.Max(Vector2.Zero, UIManager.WindowRoot.Size - window.SetSize);
        LayoutContainer.SetPosition(window, Vector2.Clamp(position, Vector2.Zero, max));
        window.MoveToFront();
    }

    private void OnPopOutGuidebook(GuidebookWindow source)
    {
        var guide = UIManager.CreateWindow<GuidebookWindow>();
        ConfigureWorkspaceWindow(guide, false);
        source.CopyWorkspaceTo(guide);
        var window = guide.OpenPopOut();
        _popOutGuideWindows.Add(window);
        window.Closed += () => _popOutGuideWindows.Remove(window);
        source.Close();
    }

    private void CloseWorkspaceWindows()
    {
        _guideWindow?.SaveWorkspace();

        foreach (var window in _popOutGuideWindows.ToArray())
            window.Close();

        foreach (var window in _additionalGuideWindows.ToArray())
            window.Dispose();

        _additionalGuideWindows.Clear();
    }
}
