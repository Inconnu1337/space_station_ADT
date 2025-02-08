using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Components;
using Content.Shared.Preferences;
using Robust.Shared.Serialization;

namespace Content.Shared.ADT.btr;

[Serializable, NetSerializable]
public sealed partial class EnterAPCDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class LeaveAPCDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class DelayLerpRotationEvent : SimpleDoAfterEvent
{
    public NetEntity Uid;
    public float FrameTime;

    public DelayLerpRotationEvent(NetEntity uid, float frameTime)
    {
        Uid = uid;
        FrameTime = frameTime;
    }
}



public sealed partial class APCControlReturnActionEvent : InstantActionEvent
{
}

public sealed class ReturnToBodyAPCEvent : EntityEventArgs
{
    public EntityUid APCController;

    public ReturnToBodyAPCEvent(EntityUid apccontroller)
    {
        APCController = apccontroller;
    }
}

public sealed class GettingAPCControlledEvent : EntityEventArgs
{
    public EntityUid User;
    public EntityUid Controller;
    public GettingAPCControlledEvent(EntityUid user, EntityUid controller)
    {
        User = user;
        Controller = controller;
    }
}

[Serializable, NetSerializable]
public enum APCVisuals : byte
{
    Destroyed
}

[Serializable, NetSerializable]
public enum APCVisualLayers : byte
{
    Base
}
