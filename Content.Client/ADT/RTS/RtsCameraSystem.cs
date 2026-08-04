using System.Numerics;
using Content.Client.ADT.RTS.Overlays;
using Content.Client.Movement.Systems;
using Content.Shared.ADT.RTS;
using Content.Shared.ADT.RTS.Components;
using Content.Shared.ADT.RTS.Input;
using Content.Shared.Movement.Components;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Client.ADT.RTS;

/// <summary>
/// Клиентская часть камеры: отслеживает вход и выход игрока из матча и отправляет
/// запрос на выход. Кламп камеры по границам арены живёт в общей системе,
/// чтобы предсказание совпадало с сервером.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §4.
/// </summary>
public sealed class RtsCameraSystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly ContentEyeSystem _contentEye = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly RtsFogSystem _fog = default!;
    [Dependency] private readonly RtsSelectionSystem _selection = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// Множитель зума на одну «щёлку» колеса. Штатный ZoomMod (1.5) для колеса слишком
    /// грубый: в RTS зумом пользуются постоянно и хочется плавности.
    /// </summary>
    private const float WheelZoomStep = 1.12f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RtsCameraComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<RtsCameraComponent, LocalPlayerDetachedEvent>(OnDetached);

        CommandBinds.Builder
            .Bind(RtsKeyFunctions.RtsLeaveMatch, InputCmdHandler.FromDelegate(_ => RequestLeaveMatch()))
            .Register<RtsCameraSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<RtsCameraSystem>();

        // Отключение от сервера посреди матча: флаг обязан сброситься, иначе после
        // переподключения игрок окажется в RTS-интерфейсе без матча.
        _ui.GetUIController<RtsHudUIController>().SetInMatch(false, reloadScreen: false);

        base.Shutdown();
    }

    private void OnAttached(EntityUid uid, RtsCameraComponent component, LocalPlayerAttachedEvent args)
    {
        _ui.GetUIController<RtsHudUIController>().SetInMatch(true);

        if (!_overlays.HasOverlay<RtsSelectionOverlay>())
            _overlays.AddOverlay(new RtsSelectionOverlay(EntityManager, _selection, _transform));

        // Туман рисуется под маркерами выделения, поэтому оверлей добавляем раньше.
        if (!_overlays.HasOverlay<RtsFogOverlay>())
            _overlays.AddOverlay(new RtsFogOverlay(_fog));
    }

    private void OnDetached(EntityUid uid, RtsCameraComponent component, LocalPlayerDetachedEvent args)
    {
        _ui.GetUIController<RtsHudUIController>().SetInMatch(false);

        _overlays.RemoveOverlay<RtsSelectionOverlay>();
        _overlays.RemoveOverlay<RtsFogOverlay>();
        _selection.Clear();
        _fog.Reset();
    }

    /// <summary>
    /// Просит сервер выпустить игрока. Решение всё равно принимает сервер.
    /// </summary>
    public void RequestLeaveMatch()
    {
        RaiseNetworkEvent(new RtsLeaveMatchEvent());
    }

    /// <summary>
    /// Зум колесом мыши.
    ///
    /// Колесо нельзя забиндить через keybinds.yml: в движке колеса нет в перечислении
    /// клавиш, оно приходит только как событие UI. Поэтому событие ловит RtsGameScreen
    /// и зовёт этот метод.
    /// </summary>
    /// <param name="delta">Прокрутка вверх положительна и приближает камеру.</param>
    public void Zoom(float delta)
    {
        if (delta == 0f)
            return;

        if (_players.LocalEntity is not { } local)
            return;

        if (!TryComp<ContentEyeComponent>(local, out var eye))
            return;

        // Меньшее значение зума — более близкая камера, отсюда знак минус.
        var factor = MathF.Pow(WheelZoomStep, -delta);

        // Именно RequestZoom, а не SetZoom напрямую: он поднимает предсказываемое событие,
        // которое применяется и на клиенте, и на сервере. Прямой SetZoom на клиенте
        // затирается следующим же состоянием сервера.
        //
        // Само значение к EyeComponent.Zoom потом подтягивает EyeLerpingSystem, поэтому
        // зум ещё и плавный.
        _contentEye.RequestZoom(local, eye.TargetZoom * factor, false, false, eye);
    }
}
