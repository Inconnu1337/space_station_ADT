using Content.Server.Administration.Logs;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Medical.Components;
using Content.Server.Power.Components;
using Content.Server.Temperature.Components;
using Content.Shared.UserInterface;
using Content.Shared.Climbing.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.MedicalScanner;
using Content.Shared.NinjaUpdate;
using Content.Shared.Power;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared.Humanoid;
using Content.Shared.Damage;
using Robust.Shared.Player;
using Content.Shared.Popups;
using Content.Shared.Chemistry.EntitySystems;
using System.Threading.Tasks;
using FastAccessors;

namespace Content.Server.NinjaUpdate;

public sealed partial class SpiderScannerSystem : SharedSpiderScannerSystem
{
    [Dependency] private readonly ClimbSystem _climbSystem = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpiderScannerComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SpiderScannerComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
        SubscribeLocalEvent<SpiderScannerComponent, SpiderScannerDragFinished>(OnDragFinished);
        SubscribeLocalEvent<SpiderScannerComponent, SpiderScannerPryFinished>(OnSpiderScannerPryFinished);

        SubscribeLocalEvent<SpiderScannerComponent, DragDropTargetEvent>(HandleDragDropOn);
        SubscribeLocalEvent<SpiderScannerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SpiderScannerComponent, ActivatableUIOpenAttemptEvent>(OnActivateUIAttempt);
        SubscribeLocalEvent<SpiderScannerComponent, AfterActivatableUIOpenEvent>(OnActivateUI);
        SubscribeLocalEvent<SpiderScannerComponent, EntRemovedFromContainerMessage>(OnEjected);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }

    public override EntityUid? EjectBody(EntityUid uid, SpiderScannerComponent? spiderScannerComponent)
    {
        if (!Resolve(uid, ref spiderScannerComponent))
            return null;
        if (spiderScannerComponent.BodyContainer.ContainedEntity is not { Valid: true } contained)
            return null;
        base.EjectBody(uid, spiderScannerComponent);
        _climbSystem.ForciblySetClimbing(contained, uid);
        return contained;
    }

    #region Interaction

    private void HandleDragDropOn(Entity<SpiderScannerComponent> entity, ref DragDropTargetEvent args)
    {
        if (entity.Comp.BodyContainer.ContainedEntity != null)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, entity.Comp.EntryDelay, new SpiderScannerDragFinished(), entity, target: args.Dragged, used: entity)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
        };
        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnDragFinished(Entity<SpiderScannerComponent> entity, ref SpiderScannerDragFinished args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (InsertBody(entity.Owner, args.Args.Target.Value, entity.Comp))
        {
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(args.User)} inserted {ToPrettyString(args.Args.Target.Value)} into {ToPrettyString(entity.Owner)}");
        }
        args.Handled = true;
    }

    private void OnActivateUIAttempt(Entity<SpiderScannerComponent> entity, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
        {
            return;
        }

        var containedEntity = entity.Comp.BodyContainer.ContainedEntity;
        if (containedEntity == null || containedEntity == args.User)
        {
            args.Cancel();
        }
    }

    private void OnActivateUI(Entity<SpiderScannerComponent> entity, ref AfterActivatableUIOpenEvent args)
    {
        if (!entity.Comp.BodyContainer.ContainedEntity.HasValue)
            return;

        TryComp<TemperatureComponent>(entity.Comp.BodyContainer.ContainedEntity, out var temp);
        TryComp<BloodstreamComponent>(entity.Comp.BodyContainer.ContainedEntity, out var bloodstream);

        if (TryComp<HealthAnalyzerComponent>(entity, out var healthAnalyzer))
        {
            healthAnalyzer.ScannedEntity = entity.Comp.BodyContainer.ContainedEntity;
        }

        // TODO: This should be a state my dude
        _userInterfaceSystem.ServerSendUiMessage(
            entity.Owner,
            HealthAnalyzerUiKey.Key,
            new HealthAnalyzerScannedUserMessage(GetNetEntity(entity.Comp.BodyContainer.ContainedEntity),
            temp?.CurrentTemperature ?? 0,
            (bloodstream != null && _solutionContainerSystem.ResolveSolution(entity.Comp.BodyContainer.ContainedEntity.Value,
                bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
                ? bloodSolution.FillFraction
                : 0,
            null,
            null
        ));
    }

    private void OnPowerChanged(Entity<SpiderScannerComponent> entity, ref PowerChangedEvent args)
    {
        // Needed to avoid adding/removing components on a deleted entity
        if (Terminating(entity))
        {
            return;
        }

        if (!args.Powered)
        {
            _uiSystem.CloseUi(entity.Owner, HealthAnalyzerUiKey.Key);
        }
        UpdateAppearance(entity.Owner, entity.Comp);
    }

    #endregion
    private void OnEjected(Entity<SpiderScannerComponent> spiderScanner, ref EntRemovedFromContainerMessage args)
    {
        if (TryComp<HealthAnalyzerComponent>(spiderScanner.Owner, out var healthAnalyzer))
        {
            healthAnalyzer.ScannedEntity = null;
        }

        // if body is ejected - no need to display health-analyzer
        _uiSystem.CloseUi(spiderScanner.Owner, HealthAnalyzerUiKey.Key);
    }
}
