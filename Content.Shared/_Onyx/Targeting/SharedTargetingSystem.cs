using Content.Shared.Body.Part;
using System.Linq;

namespace Content.Shared._Onyx.Targeting;

public abstract class SharedTargetingSystem : EntitySystem
{
    public static readonly TargetBodyPart[] SelectableParts =
    [
        TargetBodyPart.Head,
        TargetBodyPart.Chest,
        TargetBodyPart.Groin,
        TargetBodyPart.LeftArm,
        TargetBodyPart.LeftHand,
        TargetBodyPart.RightArm,
        TargetBodyPart.RightHand,
        TargetBodyPart.LeftLeg,
        TargetBodyPart.LeftFoot,
        TargetBodyPart.RightLeg,
        TargetBodyPart.RightFoot,
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TargetingComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(Entity<TargetingComponent> ent, ref ComponentInit args)
    {
        if (!IsSelectable(ent.Comp.Target))
            ent.Comp.Target = TargetBodyPart.Chest;

        foreach (var (requested, outcomes) in ent.Comp.TargetOdds.ToArray())
        {
            if (!IsSelectable(requested))
            {
                ent.Comp.TargetOdds.Remove(requested);
                continue;
            }

            foreach (var (outcome, weight) in outcomes.ToArray())
            {
                if (!IsSelectable(outcome) || !float.IsFinite(weight) || weight < 0f)
                    outcomes.Remove(outcome);
            }

            if (outcomes.Count == 0 || outcomes.Values.Sum() <= 0f)
                ent.Comp.TargetOdds[requested] = new() { [requested] = 1f };
        }
    }

    public static bool IsSelectable(TargetBodyPart part) => part != 0 && (part & (part - 1)) == 0 && (part & TargetBodyPart.All) != 0;

    public static bool TryConvert(TargetBodyPart target, out BodyPartType type, out BodyPartSymmetry symmetry)
    {
        (type, symmetry) = target switch
        {
            TargetBodyPart.Head => (BodyPartType.Head, BodyPartSymmetry.None),
            TargetBodyPart.Chest => (BodyPartType.Chest, BodyPartSymmetry.None),
            TargetBodyPart.Groin => (BodyPartType.Groin, BodyPartSymmetry.None),
            TargetBodyPart.LeftArm => (BodyPartType.Arm, BodyPartSymmetry.Left),
            TargetBodyPart.RightArm => (BodyPartType.Arm, BodyPartSymmetry.Right),
            TargetBodyPart.LeftHand => (BodyPartType.Hand, BodyPartSymmetry.Left),
            TargetBodyPart.RightHand => (BodyPartType.Hand, BodyPartSymmetry.Right),
            TargetBodyPart.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
            TargetBodyPart.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
            TargetBodyPart.LeftFoot => (BodyPartType.Foot, BodyPartSymmetry.Left),
            TargetBodyPart.RightFoot => (BodyPartType.Foot, BodyPartSymmetry.Right),
            _ => default,
        };
        return IsSelectable(target);
    }

    public static bool TryConvert(BodyPartType type, BodyPartSymmetry symmetry, out TargetBodyPart target)
    {
        target = (type, symmetry) switch
        {
            (BodyPartType.Head, _) => TargetBodyPart.Head,
            (BodyPartType.Chest, _) => TargetBodyPart.Chest,
            (BodyPartType.Groin, _) => TargetBodyPart.Groin,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => TargetBodyPart.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => TargetBodyPart.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => TargetBodyPart.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => TargetBodyPart.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => TargetBodyPart.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => TargetBodyPart.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => TargetBodyPart.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => TargetBodyPart.RightFoot,
            _ => 0,
        };
        return target != 0;
    }
}
