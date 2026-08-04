using System.Linq;
using System.Numerics;
using Content.Shared.ADT.RTS;
using Content.Shared.ADT.RTS.Components;
using Content.Shared.ADT.RTS.Input;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.ADT.RTS;

/// <summary>
/// Выделение и приказы. Выделение целиком клиентское: сервер о нём не знает и
/// перепроверяет владение на каждом приказе.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §4.2.
/// </summary>
public sealed class RtsSelectionSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <summary>
    /// Короче этой протяжки жест считается кликом, а не рамкой.
    /// </summary>
    private const float DragThreshold = 0.4f;

    /// <summary>
    /// Радиус подбора сущности под курсором при одиночном клике.
    /// </summary>
    private const float ClickRadius = 0.6f;

    /// <summary>
    /// Выделенные сущности.
    /// </summary>
    public readonly List<EntityUid> Selected = new();

    /// <summary>
    /// Мировая точка, с которой началась протяжка рамки. Null — рамку не тянут.
    /// </summary>
    public Vector2? DragStart { get; private set; }

    /// <summary>
    /// Карта, на которой начали тянуть рамку.
    /// </summary>
    public MapId DragMap { get; private set; } = MapId.Nullspace;

    /// <summary>
    /// Выделение изменилось: панель выделения перерисовывается по этому событию.
    /// </summary>
    public event Action? SelectionChanged;

    private readonly HashSet<Entity<RtsSelectableComponent>> _lookupResults = new();

    public override void Initialize()
    {
        base.Initialize();

        // Мышь идёт обычным мировым путём: ScalingViewport внутри MainViewport перехватывает
        // клик хит-тестом и сам пробрасывает его сюда через ViewportKeyEvent. Обрабатывать
        // мышь в самом RtsGameScreen нельзя — _doGuiInput обрывается на ScalingViewport,
        // у которого MouseFilter = Stop, и до экрана событие не доходит.
        // outsidePrediction: true обязателен. У Pointer-обработчиков он по умолчанию false,
        // а InputSystem.HandleInputCommand пропускает такие обработчики, когда предсказание
        // выключено: `if (!IsPredictionEnabled && !handler.FireOutsidePrediction) continue;`.
        // У InputCmdHandler.FromDelegate дефолт наоборот true — отсюда и брался симптом
        // «клавиши работают, мышь молчит». Весь контентный клиентский код ставит его явно.
        CommandBinds.Builder
            .Bind(RtsKeyFunctions.RtsSelect, new PointerStateInputCmdHandler(OnSelectDown, OnSelectUp, outsidePrediction: true))
            .Bind(RtsKeyFunctions.RtsOrder, new PointerInputCmdHandler(OnOrder, outsidePrediction: true))
            .Bind(RtsKeyFunctions.RtsStop, InputCmdHandler.FromDelegate(_ => SendSimpleOrder(RtsOrderType.Stop)))
            .Bind(RtsKeyFunctions.RtsHoldPosition, InputCmdHandler.FromDelegate(_ => SendSimpleOrder(RtsOrderType.HoldPosition)))
            .Register<RtsSelectionSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<RtsSelectionSystem>();
        Clear();

        base.Shutdown();
    }

    /// <summary>
    /// Команда локального игрока: индекс и сущность.
    ///
    /// Держим оба идентификатора и сверяем по любому из них. Индекс надёжнее — это обычное
    /// число, — но сущность команды живёт в нулевом пространстве, и полагаться только на
    /// неё нельзя. Если доехало хоть что-то одно, выделение уже работает.
    /// </summary>
    public (int Index, EntityUid? Team) GetLocalTeam()
    {
        if (_players.LocalEntity is not { } local)
            return (-1, null);

        if (!TryComp<RtsCameraComponent>(local, out var camera))
            return (-1, null);

        return (camera.TeamIndex, camera.Team);
    }

    private bool OnSelectDown(ICommonSession? session, EntityCoordinates coordinates, EntityUid uid)
    {
        // Намеренно не проверяем здесь команду. Раньше проверка стояла на нажатии, и если
        // состояние камеры ещё не доехало, жест обрывался в самом начале: DragStart не
        // выставлялся, отпускание уходило в никуда, и весь ввод выглядел мёртвым без
        // единой строчки в логе. Проверка нужна там, где реально решается «своё или чужое».
        var map = _xform.ToMapCoordinates(coordinates);
        DragStart = map.Position;
        DragMap = map.MapId;
        return true;
    }

    /// <summary>
    /// Отпустили ЛКМ: короткий жест — клик, длинный — рамка.
    /// </summary>
    private bool OnSelectUp(ICommonSession? session, EntityCoordinates coordinates, EntityUid uid)
    {
        if (DragStart is not { } start)
            return false;

        DragStart = null;

        var team = GetLocalTeam();

        // Бросаем жест, только если не доехал ни индекс команды, ни её сущность.
        if (team.Index < 0 && team.Team == null)
            return false;

        var map = _xform.ToMapCoordinates(coordinates);

        if (!IsShiftHeld())
            Selected.Clear();

        if ((map.Position - start).Length() < DragThreshold)
            SelectAt(map, team);
        else
            SelectInBox(map.MapId, team, Box2.FromTwoPoints(start, map.Position));

        SelectionChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Нажали ПКМ: контекстный приказ выделенным.
    /// </summary>
    private bool OnOrder(ICommonSession? session, EntityCoordinates coordinates, EntityUid uid)
    {
        if (Selected.Count == 0)
            return false;

        RaiseNetworkEvent(new RtsOrderEvent
        {
            Units = GetNetEntityArray(),
            Type = RtsOrderType.Contextual,
            Target = GetNetCoordinates(coordinates),
            Queue = IsShiftHeld(),
        });

        return true;
    }

    private void SelectAt(MapCoordinates coordinates, (int Index, EntityUid? Team) team)
    {
        _lookupResults.Clear();
        _lookup.GetEntitiesInRange(coordinates, ClickRadius, _lookupResults);

        EntityUid? best = null;
        var bestPriority = int.MinValue;

        foreach (var candidate in _lookupResults)
        {
            if (!IsOwnedBy(candidate.Owner, team) || candidate.Comp.Priority <= bestPriority)
                continue;

            bestPriority = candidate.Comp.Priority;
            best = candidate.Owner;
        }

        if (best is not { } selected || Selected.Contains(selected))
            return;

        Selected.Add(selected);
    }

    private void SelectInBox(MapId mapId, (int Index, EntityUid? Team) team, Box2 box)
    {
        // Лукап работает по радиусу, поэтому берём описанную окружность и отсеиваем лишнее.
        var center = box.Center;
        var radius = (box.TopRight - center).Length();

        _lookupResults.Clear();
        _lookup.GetEntitiesInRange(mapId, center, radius, _lookupResults);

        foreach (var candidate in _lookupResults)
        {
            if (!IsOwnedBy(candidate.Owner, team))
                continue;

            if (!box.Contains(_xform.GetWorldPosition(candidate.Owner)) || Selected.Contains(candidate.Owner))
                continue;

            Selected.Add(candidate.Owner);
        }
    }

    private bool IsOwnedBy(EntityUid uid, (int Index, EntityUid? Team) team)
    {
        if (!TryComp<RtsOwnedComponent>(uid, out var owned))
            return false;

        if (team.Index >= 0 && owned.TeamIndex == team.Index)
            return true;

        return team.Team != null && owned.Team == team.Team;
    }

    private void SendSimpleOrder(RtsOrderType type)
    {
        if (Selected.Count == 0)
            return;

        RaiseNetworkEvent(new RtsOrderEvent
        {
            Units = GetNetEntityArray(),
            Type = type,
        });
    }

    private NetEntity[] GetNetEntityArray()
    {
        // Удалённые сущности отсеиваем здесь, чтобы не гонять мусор по сети.
        Selected.RemoveAll(entity => !Exists(entity));
        return Selected.Select(entity => GetNetEntity(entity)).ToArray();
    }

    public void Clear()
    {
        if (Selected.Count == 0)
            return;

        Selected.Clear();
        SelectionChanged?.Invoke();
    }

    private bool IsShiftHeld()
    {
        return _input.IsKeyDown(Keyboard.Key.Shift);
    }

    /// <summary>
    /// Текущая рамка выделения в мировых координатах. Считается на лету от точки начала
    /// протяжки до курсора, поэтому отдельно нигде не хранится.
    /// </summary>
    public Box2? GetDragBox()
    {
        if (DragStart is not { } start)
            return null;

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);

        if (mouse.MapId != DragMap)
            return null;

        var box = Box2.FromTwoPoints(start, mouse.Position);

        if (box.Width < DragThreshold && box.Height < DragThreshold)
            return null;

        return box;
    }
}
