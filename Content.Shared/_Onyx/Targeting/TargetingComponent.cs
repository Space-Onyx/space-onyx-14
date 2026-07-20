using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Targeting;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class TargetingComponent : Component
{
    [DataField, AutoNetworkedField]
    public TargetBodyPart Target = TargetBodyPart.Chest;

    [DataField]
    public Dictionary<TargetBodyPart, Dictionary<TargetBodyPart, float>> TargetOdds = DefaultOdds();

    public static Dictionary<TargetBodyPart, Dictionary<TargetBodyPart, float>> DefaultOdds() => new()
    {
        [TargetBodyPart.Head] = new() { [TargetBodyPart.Head] = 0.5f, [TargetBodyPart.Chest] = 0.5f },
        [TargetBodyPart.Chest] = new()
        {
            [TargetBodyPart.Chest] = 0.6f,
            [TargetBodyPart.Head] = 0.1f,
            [TargetBodyPart.LeftArm] = 0.075f,
            [TargetBodyPart.RightArm] = 0.075f,
            [TargetBodyPart.LeftLeg] = 0.075f,
            [TargetBodyPart.RightLeg] = 0.075f,
        },
        [TargetBodyPart.Groin] = new()
        {
            [TargetBodyPart.Groin] = 0.6f,
            [TargetBodyPart.Head] = 0.1f,
            [TargetBodyPart.LeftArm] = 0.075f,
            [TargetBodyPart.RightArm] = 0.075f,
            [TargetBodyPart.LeftLeg] = 0.075f,
            [TargetBodyPart.RightLeg] = 0.075f,
        },
        [TargetBodyPart.LeftArm] = new() { [TargetBodyPart.LeftArm] = 0.7f, [TargetBodyPart.LeftHand] = 0.15f, [TargetBodyPart.Chest] = 0.15f },
        [TargetBodyPart.RightArm] = new() { [TargetBodyPart.RightArm] = 0.7f, [TargetBodyPart.RightHand] = 0.15f, [TargetBodyPart.Chest] = 0.15f },
        [TargetBodyPart.LeftHand] = new() { [TargetBodyPart.LeftHand] = 0.35f, [TargetBodyPart.LeftArm] = 0.65f },
        [TargetBodyPart.RightHand] = new() { [TargetBodyPart.RightHand] = 0.35f, [TargetBodyPart.RightArm] = 0.65f },
        [TargetBodyPart.LeftLeg] = new() { [TargetBodyPart.LeftLeg] = 0.7f, [TargetBodyPart.LeftFoot] = 0.15f, [TargetBodyPart.Chest] = 0.15f },
        [TargetBodyPart.RightLeg] = new() { [TargetBodyPart.RightLeg] = 0.7f, [TargetBodyPart.RightFoot] = 0.15f, [TargetBodyPart.Chest] = 0.15f },
        [TargetBodyPart.LeftFoot] = new() { [TargetBodyPart.LeftFoot] = 0.35f, [TargetBodyPart.LeftLeg] = 0.65f },
        [TargetBodyPart.RightFoot] = new() { [TargetBodyPart.RightFoot] = 0.35f, [TargetBodyPart.RightLeg] = 0.65f },
    };
}
