using Content.Shared.Interaction;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Toggleable;
using Content.Shared.Tools.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Salvage.MarkerBeacon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MarkerBeaconColorPickerComponent : Component
{
    [DataField, AutoNetworkedField] public Color ActivatedColor = Color.White;
    [DataField] public List<Color> ColorOptions =
        new() { Color.Tomato, Color.DodgerBlue, Color.Aqua, Color.MediumSpringGreen, Color.MediumOrchid };
    [DataField, AutoNetworkedField] public bool Hacked;
    [DataField] public float CycleRate = 1f;
    public HashSet<EntityUid> AuthorizedHackers = new();
}

[Serializable, NetSerializable]
public enum MarkerBeaconColorPickerUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class MarkerBeaconColorChangedMessage(Color color) : BoundUserInterfaceMessage
{
    public Color Color = color;
}

[Serializable, NetSerializable]
public sealed class MarkerBeaconHackedChangedMessage(bool hacked) : BoundUserInterfaceMessage
{
    public bool Hacked = hacked;
}

public sealed partial class MarkerBeaconColorPickerSystem : EntitySystem
{
    [Dependency] private SharedRgbLightControllerSystem _rgb = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MarkerBeaconColorPickerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MarkerBeaconColorPickerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MarkerBeaconColorPickerComponent, MarkerBeaconColorChangedMessage>(OnColorChanged);
        SubscribeLocalEvent<MarkerBeaconColorPickerComponent, MarkerBeaconHackedChangedMessage>(OnHackedChanged);
    }

    private void OnMapInit(Entity<MarkerBeaconColorPickerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.ColorOptions.Count > 0)
            ent.Comp.ActivatedColor = _random.Pick(ent.Comp.ColorOptions);
        SetColor(ent, ent.Comp.ActivatedColor);
        if (ent.Comp.Hacked)
            _rgb.SetCycleRate(ent, ent.Comp.CycleRate, EnsureComp<RgbLightControllerComponent>(ent));
    }

    private void OnInteractUsing(Entity<MarkerBeaconColorPickerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tools.HasQuality(args.Used, SharedToolSystem.PulseQuality))
            return;
        if (_net.IsServer)
            ent.Comp.AuthorizedHackers.Add(args.User);
        _ui.TryToggleUi(ent.Owner, MarkerBeaconColorPickerUiKey.Key, args.User);
        args.Handled = true;
    }

    private void OnColorChanged(Entity<MarkerBeaconColorPickerComponent> ent, ref MarkerBeaconColorChangedMessage args)
    {
        ent.Comp.ActivatedColor = args.Color;
        SetColor(ent, args.Color);
    }

    private void OnHackedChanged(Entity<MarkerBeaconColorPickerComponent> ent, ref MarkerBeaconHackedChangedMessage args)
    {
        if (_net.IsServer && !ent.Comp.AuthorizedHackers.Remove(args.Actor))
            return;

        ent.Comp.Hacked = args.Hacked;
        if (args.Hacked)
            _rgb.SetCycleRate(ent, ent.Comp.CycleRate, EnsureComp<RgbLightControllerComponent>(ent));
        else
            RemComp<RgbLightControllerComponent>(ent);
        Dirty(ent);
    }

    private void SetColor(Entity<MarkerBeaconColorPickerComponent> ent, Color color)
    {
        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.SetData(ent, ToggleableVisuals.Color, color, appearance);
        Dirty(ent);
    }
}
