using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Targeting;

[Flags, Serializable, NetSerializable]
public enum TargetBodyPart : ushort
{
    Head = 1,
    Chest = 1 << 1,
    Groin = 1 << 2,
    LeftArm = 1 << 3,
    LeftHand = 1 << 4,
    RightArm = 1 << 5,
    RightHand = 1 << 6,
    LeftLeg = 1 << 7,
    LeftFoot = 1 << 8,
    RightLeg = 1 << 9,
    RightFoot = 1 << 10,

    Hands = LeftHand | RightHand,
    Arms = LeftArm | RightArm,
    Legs = LeftLeg | RightLeg,
    Feet = LeftFoot | RightFoot,
    FullArms = Arms | Hands,
    FullLegs = Legs | Feet,
    BodyMiddle = Chest | Groin | FullArms,
    FullLegsGroin = FullLegs | Groin,
    Vital = Head | Chest | Groin,
    All = Head | Chest | Groin | FullArms | FullLegs,
}
