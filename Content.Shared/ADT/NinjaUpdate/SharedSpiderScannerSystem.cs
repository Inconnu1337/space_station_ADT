using Content.Server.Medical.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emag.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;
using Content.Shared.Humanoid;
using Content.Shared.Damage;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Threading.Tasks;

namespace Content.Shared.NinjaUpdate;

public abstract partial class SharedSpiderScannerSystem: EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly StandingStateSystem _standingStateSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpiderScannerComponent, CanDropTargetEvent>(OnSpiderScannerCanDropOn);
        InitializeInsideSpiderScanner();
    }

    private void OnSpiderScannerCanDropOn(EntityUid uid, SpiderScannerComponent component, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = HasComp<BodyComponent>(args.Dragged);
        args.Handled = true;
    }

    protected void OnComponentInit(EntityUid uid, SpiderScannerComponent spiderScannerComponent, ComponentInit args)
    {
        spiderScannerComponent.BodyContainer = _containerSystem.EnsureContainer<ContainerSlot>(uid, "scanner-body");
    }

    protected void UpdateAppearance(EntityUid uid, SpiderScannerComponent? spiderScanner = null, AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref spiderScanner))
            return;

        if (!Resolve(uid, ref appearance))
            return;

        _appearanceSystem.SetData(uid, SpiderScannerComponent.SpiderScannerVisuals.ContainsEntity, spiderScanner.BodyContainer.ContainedEntity == null, appearance);
        _appearanceSystem.SetData(uid, SpiderScannerComponent.SpiderScannerVisuals.IsOn, spiderScanner.BodyContainer.ContainedEntity != null, appearance);
    }

    public bool InsertBody(EntityUid uid, EntityUid target, SpiderScannerComponent spiderScannerComponent)
    {
        Log.Debug("Дошло сюда");
        if (spiderScannerComponent.BodyContainer.ContainedEntity != null)
            return false;

        if (!HasComp<MobStateComponent>(target))
            return false;

        if (!HasComp<HumanoidAppearanceComponent>(target))
            return false;

        if (TryComp<DamageableComponent>(target, out var damageable))
        {
            Log.Debug("Дошло и сюда");
            if (damageable.Damage.DamageDict.TryGetValue("Cellular", out var damage) && damage >= 70)
            {
                //EnsureComp
                _popupSystem.PopupEntity("Обнаружена поврежденная цепочка ДНК. Сканирование невозможно.", target, uid);
                return false;
            }

            if (damageable.TotalDamage > 0)
            {
                _popupSystem.PopupEntity("Существо не может быть отсканировано, оно повреждено.", target, uid);
                return false;
            }
        }

        var xform = Transform(target);
        _containerSystem.Insert((target, xform), spiderScannerComponent.BodyContainer);

        EnsureComp<InsideSpiderScannerComponent>(target);
        _standingStateSystem.Stand(target, force: true);

        UpdateAppearance(uid, spiderScannerComponent);
        StartScanningProcess(uid, spiderScannerComponent);
        return true;
    }

    public void TryEjectBody(EntityUid uid, EntityUid userId, SpiderScannerComponent? spiderScannerComponent)
    {
        if (!Resolve(uid, ref spiderScannerComponent))
            return;

        var ejected = EjectBody(uid, spiderScannerComponent);
        if (ejected != null)
            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ejected.Value)} ejected from {ToPrettyString(uid)} by {ToPrettyString(userId)}");
    }

    /// <summary>
    /// Ejects the contained body
    /// </summary>
    /// <param name="uid">The cryopod entity</param>
    /// <param name="spiderScannerComponent">Cryopod component of <see cref="uid"/></param>
    /// <returns>Ejected entity</returns>
    public virtual EntityUid? EjectBody(EntityUid uid, SpiderScannerComponent? spiderScannerComponent)
    {
        if (!Resolve(uid, ref spiderScannerComponent))
            return null;

        if (spiderScannerComponent.BodyContainer.ContainedEntity is not {Valid: true} contained)
            return null;

        _containerSystem.Remove(contained, spiderScannerComponent.BodyContainer);

        // Restore the correct position of the patient. Checking the components manually feels hacky, but I did not find a better way for now.
        if (HasComp<KnockedDownComponent>(contained) || _mobStateSystem.IsIncapacitated(contained))
        {
            _standingStateSystem.Down(contained);
        }
        else
        {
            _standingStateSystem.Stand(contained);
        }

        UpdateAppearance(uid, spiderScannerComponent);
        return contained;
    }

    protected void AddAlternativeVerbs(EntityUid uid, SpiderScannerComponent spiderScannerComponent, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Eject verb
        if (spiderScannerComponent.BodyContainer.ContainedEntity != null)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cryo-pod-verb-noun-occupant"),
                Category = VerbCategory.Eject,
                Priority = 1, // Promote to top to make ejecting the ALT-click action
                Act = () => TryEjectBody(uid, args.User, spiderScannerComponent)
            });
        }
    }

    protected void OnSpiderScannerPryFinished(EntityUid uid, SpiderScannerComponent spiderScannerComponent, SpiderScannerPryFinished args)
    {
        if (args.Cancelled)
            return;

        var ejected = EjectBody(uid, spiderScannerComponent);
        if (ejected != null)
            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ejected.Value)} pried out of {ToPrettyString(uid)} by {ToPrettyString(args.User)}");
    }

    private void StartScanningProcess(EntityUid spiderScanner, SpiderScannerComponent component)
    {
        if (component.IsScanning)
            return;

        component.IsScanning = true;
        _popupSystem.PopupEntity("Начато сканирование... Пожалуйста, подождите.", spiderScanner, spiderScanner);
        Log.Debug("Сканирование начато");

        Timer.Spawn(TimeSpan.FromMinutes(1), () =>
        {
            _popupSystem.PopupEntity("До завершения сканирования осталась 1 минута!", spiderScanner, spiderScanner);

            Timer.Spawn(TimeSpan.FromMinutes(1), () =>
            {
                component.IsScanning = false;
                _popupSystem.PopupEntity("Сканирование завершено.", spiderScanner, spiderScanner);
                if (!component.IsScanning && component.BodyContainer.ContainedEntity is { Valid: true } containedEntity)
                {
                    TryEjectBody(spiderScanner, containedEntity, component);
                }
            });
        });
    }

    [Serializable, NetSerializable]
    public sealed partial class SpiderScannerPryFinished : SimpleDoAfterEvent
    {
    }

    [Serializable, NetSerializable]
    public sealed partial class SpiderScannerDragFinished : SimpleDoAfterEvent
    {
    }
}
