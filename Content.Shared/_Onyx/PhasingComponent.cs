using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Phasing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PhasingComponent : Component
{
    /// <summary>
    /// Включен ли эффект фазирования.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = true;

    /// <summary>
    /// Скорость анимации эффекта фазирования.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float AnimationSpeed = 1.1f;

    /// <summary>
    /// Сила сдвигов (множитель для смещений).
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float DistortionStrength = 1.0f;

    /// <summary>
    /// Минимальное количество полос деления.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float BandMin = 3.0f;

    /// <summary>
    /// Максимальное количество полос деления.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float BandMax = 8.0f;

    /// <summary>
    /// Частота появления глюков (0.0 - 1.0).
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float GlitchFrequency = 0.7f;

    /// <summary>
    /// Сила разрыва полос (0.0 - 1.0).
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float BandSplitStrength = 0.3f;

    /// <summary>
    /// Частота разрыва полос (0.0 - 1.0).
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float BandSplitFrequency = 0.85f;
}
