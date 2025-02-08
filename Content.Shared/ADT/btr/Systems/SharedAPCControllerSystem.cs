using System.Linq;
using Content.Shared.ADT.btr.Systems;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Mind;

namespace Content.Shared.ADT.btr;

public sealed partial class SharedAPCControllerSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<APCControllerComponent, InteractHandEvent>(AfterInteract);
        SubscribeLocalEvent<APCControllerComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<APCControllerComponent, ReturnToBodyAPCEvent>(OnReturn);
    }

    public void OnReturn(EntityUid uid, APCControllerComponent component, ReturnToBodyAPCEvent args)
    {
        component.CurrentUser = null;
        component.CurrentAPC = null;
    }
    public void AfterInteract(EntityUid uid, APCControllerComponent component, InteractHandEvent args)
    {
        if (TryComp<GhostComponent>(args.User, out var _))
            return;

        if (!TryComp<APCPilotComponent>(args.User, out var pilot))
            return;

        var target = pilot.APC;
        if (target == null)
            return;

        component.CurrentUser = args.User;
        component.CurrentAPC = target;
        RaiseLocalEvent(target.Value, new GettingAPCControlledEvent(args.User, uid));
        _mindSystem.ControlMob(args.User, target.Value);
    }

    public void OnShutdown(EntityUid uid, APCControllerComponent component, ComponentShutdown args)
    {
        if (component.CurrentUser != null && component.CurrentAPC != null)
        {
            if (!TryComp<APCEntityComponent>(component.CurrentAPC, out var apcComp))
                return;

            if (apcComp.User != null)
                _mindSystem.ControlMob((EntityUid)component.CurrentAPC, (EntityUid)component.CurrentAPC);
        }
    }
}
