// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Movement.Systems;

public sealed partial class RefreshMovementSpeedModifiersEvent
{
    private float _walkSpeedup = 1.0f;
    private float _walkSlowdown = 1.0f;
    private float _sprintSpeedup = 1.0f;
    private float _sprintSlowdown = 1.0f;
    private bool _slowdownImmune;

    public float WalkSpeedModifier => _walkSpeedup * (_slowdownImmune ? 1.0f : _walkSlowdown);
    public float SprintSpeedModifier => _sprintSpeedup * (_slowdownImmune ? 1.0f : _sprintSlowdown);

    public void ModifySpeed(float walk, float sprint)
    {
        if (walk < 1.0f)
            _walkSlowdown *= walk;
        else
            _walkSpeedup *= walk;

        if (sprint < 1.0f)
            _sprintSlowdown *= sprint;
        else
            _sprintSpeedup *= sprint;
    }

    public void ModifySpeed(float mod)
    {
        ModifySpeed(mod, mod);
    }

    public void GrantSlowdownImmunity()
    {
        _slowdownImmune = true;
    }
}
