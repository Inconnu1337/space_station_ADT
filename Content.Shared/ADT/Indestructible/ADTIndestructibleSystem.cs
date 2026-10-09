using Content.Shared.Construction.Components;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Content.Shared.Prying.Components;
using Content.Shared.Prying.Systems;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;

namespace Content.Shared.ADT.Indestructible;

public sealed class ADTIndestructibleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ADTIndestructibleComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<ADTIndestructibleComponent, DestructionAttemptEvent>(OnDestructionAttempt);
        SubscribeLocalEvent<ADTIndestructibleComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
        SubscribeLocalEvent<ADTIndestructibleComponent, BeforePryEvent>(OnBeforePry);
        SubscribeLocalEvent<ADTIndestructibleComponent, WeldableAttemptEvent>(OnWeldAttempt);
        SubscribeLocalEvent<ADTIndestructibleComponent, AttemptChangePanelEvent>(OnPanelAttempt);
        SubscribeLocalEvent<ADTIndestructibleComponent, InteractUsingEvent>(OnInteractUsing,
            before: new[] { typeof(AnchorableSystem), typeof(PryingSystem), typeof(WeldableSystem) });
    }

    private void OnBeforeDamage(Entity<ADTIndestructibleComponent> ent, ref BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
    }

    private void OnDestructionAttempt(Entity<ADTIndestructibleComponent> ent, ref DestructionAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnUnanchorAttempt(Entity<ADTIndestructibleComponent> ent, ref UnanchorAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnBeforePry(Entity<ADTIndestructibleComponent> ent, ref BeforePryEvent args)
    {
        args.Cancelled = true;
    }

    private void OnWeldAttempt(Entity<ADTIndestructibleComponent> ent, ref WeldableAttemptEvent args)
    {
        if (!ent.Comp.AllowWelding)
            args.Cancel();
    }

    private void OnPanelAttempt(Entity<ADTIndestructibleComponent> ent, ref AttemptChangePanelEvent args)
    {
        args.Cancelled = true;
    }

    private void OnInteractUsing(Entity<ADTIndestructibleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.BlockItemInteractions)
            return;

        args.Handled = true;
    }
}
