using Content.Shared.Actions;
using Content.Shared.ADT.Salvage.Components;
using Content.Shared.ADT.Silicon;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.SSDIndicator;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.ADT.Salvage.Systems;

public sealed class SharedCursedHeartSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly NetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CursedHeartAffectedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CursedHeartAffectedComponent, PumpHeartActionEvent>(OnPump);
        SubscribeLocalEvent<CursedHeartAffectedComponent, ToggleHeartActionEvent>(OnToggle);

        SubscribeLocalEvent<CursedHeartComponent, UseInHandEvent>(OnUseInHand);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CursedHeartAffectedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (comp.IsStopped)
                continue;

            if (TryComp<SSDIndicatorComponent>(uid, out var ssd) && ssd.IsSSD)
                continue;

            if (TryComp<MindContainerComponent>(uid, out var mindContainer) && mindContainer.Mind == null)
                continue;

            if (_timing.CurTime >= comp.LastPumped + TimeSpan.FromSeconds(comp.PumpIntervalSeconds))
            {
                Damage(uid, comp);
                comp.LastPumped = _timing.CurTime;
                Dirty(uid, comp);
            }
        }
    }

    private void Damage(EntityUid uid, CursedHeartAffectedComponent comp)
    {
        _bloodstream.TryModifyBloodLevel(uid, comp.BloodDamageOnMiss);
        _popup.PopupClient(Loc.GetString("popup-cursed-heart-damage"), uid, uid, PopupType.MediumCaution);
    }

    private void OnShutdown(EntityUid uid, CursedHeartAffectedComponent comp, ComponentShutdown args)
    {
        foreach (var actionEntity in comp.Actions.Values)
        {
            _actions.RemoveAction(uid, actionEntity);
        }
        comp.Actions.Clear();
    }

    private void OnPump(EntityUid uid, CursedHeartAffectedComponent comp, PumpHeartActionEvent args)
    {
        if (args.Handled || comp.IsStopped)
            return;

        args.Handled = true;

        _audio.PlayPredicted(comp.HeartbeatSound, uid, uid);

        if (comp.PumpHealing != null)
        {
            _damage.TryChangeDamage(uid, comp.PumpHealing, true, false);
        }

        _bloodstream.TryModifyBloodLevel(uid, comp.BloodRegenOnPump);
        comp.LastPumped = _timing.CurTime;
        Dirty(uid, comp);
    }

    private void OnToggle(EntityUid uid, CursedHeartAffectedComponent comp, ToggleHeartActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (comp.IsStopped)
        {
            comp.IsStopped = false;
            _popup.PopupClient(Loc.GetString("popup-cursed-heart-start"), uid, uid, PopupType.Large);
            _audio.PlayPredicted(comp.HeartbeatSound, uid, uid);
        }
        else
        {
            comp.IsStopped = true;
            _popup.PopupClient(Loc.GetString("popup-cursed-heart-stop"), uid, uid, PopupType.LargeCaution);
            _audio.PlayPredicted(comp.HeartbeatSound, uid, uid);
        }

        Dirty(uid, comp);
    }

    private void OnUseInHand(EntityUid uid, CursedHeartComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;

        if (!HasComp<BloodstreamComponent>(user) || !HasComp<HumanoidProfileComponent>(user) || HasComp<MobIpcComponent>(user))
        {
            _popup.PopupClient(Loc.GetString("popup-cursed-heart-bloodstream"), user, user, PopupType.Medium);
            return;
        }

        if (HasComp<CursedHeartAffectedComponent>(user))
        {
            _popup.PopupClient(Loc.GetString("popup-cursed-heart-already-cursed"), user, user, PopupType.Medium);
            return;
        }

        args.Handled = true;

        _popup.PopupClient(Loc.GetString("popup-cursed-heart-use"), user, user, PopupType.LargeCaution);
        _audio.PlayPredicted(comp.HeartbeatSound, user, user);

        if (comp.UseDamage != null)
        {
            _damage.TryChangeDamage(user, comp.UseDamage, true, false);
        }

        var affected = new CursedHeartAffectedComponent()
        {
            PumpIntervalSeconds = comp.PumpIntervalSeconds,
            BloodDamageOnMiss = comp.BloodDamageOnMiss,
            BloodRegenOnPump = comp.BloodRegenOnPump,
            PumpHealing = comp.PumpHealing,
            HeartbeatSound = comp.HeartbeatSound,
            LastPumped = _timing.CurTime,
        };

        AddComp(user, affected);

        foreach (var actionId in comp.Actions)
        {
            EntityUid? actionEntity = null;
            if (_actions.AddAction(user, ref actionEntity, actionId))
            {
                affected.Actions[actionId] = actionEntity.Value;
            }
        }

        Dirty(user, affected);
        PredictedQueueDel(uid);
    }
}

public sealed partial class PumpHeartActionEvent : InstantActionEvent;
public sealed partial class ToggleHeartActionEvent : InstantActionEvent;
