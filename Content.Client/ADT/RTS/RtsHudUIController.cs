using Content.Client.ADT.RTS.UI;
using Content.Client.Gameplay;
using Content.Shared.ADT.RTS.Components;
using Content.Client.UserInterface.Systems.Gameplay;
using Robust.Client.State;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.ADT.RTS;

/// <summary>
/// Владеет подменой игрового экрана на RTS-интерфейс и обратно.
/// Сам факт «игрок в матче» приходит из <see cref="RtsCameraSystem"/>.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §5.1.
/// </summary>
public sealed class RtsHudUIController : UIController, IOnSystemChanged<RtsCameraSystem>, IOnSystemChanged<RtsSelectionSystem>
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IStateManager _state = default!;

    private RtsCameraSystem? _cameras;
    private RtsSelectionSystem? _selection;

    /// <summary>
    /// Читается апстримным <see cref="GameplayState"/> при выборе экрана.
    /// </summary>
    public bool IsInMatch { get; private set; }

    public RtsGameScreen? Screen => UIManager.ActiveScreen as RtsGameScreen;

    public override void Initialize()
    {
        base.Initialize();

        var load = UIManager.GetUIController<GameplayStateLoadController>();
        load.OnScreenLoad += OnScreenLoad;
    }

    public void OnSystemLoaded(RtsCameraSystem system)
    {
        _cameras = system;
    }

    public void OnSystemUnloaded(RtsCameraSystem system)
    {
        _cameras = null;
    }

    public void OnSystemLoaded(RtsSelectionSystem system)
    {
        _selection = system;
        system.SelectionChanged += UpdateSelectionPanel;
    }

    public void OnSystemUnloaded(RtsSelectionSystem system)
    {
        system.SelectionChanged -= UpdateSelectionPanel;
        _selection = null;
    }

    /// <summary>
    /// Панель выделения. Пока показывает один юнит: сетка портретов для группы —
    /// задача фазы 9, когда юнитов станет много и это начнёт иметь смысл.
    /// </summary>
    private void UpdateSelectionPanel()
    {
        if (Screen is not { } screen)
            return;

        if (_selection == null || _selection.Selected.Count == 0)
        {
            screen.Selection.Clear();
            return;
        }

        var first = _selection.Selected[0];

        if (!_entManager.TryGetComponent<MetaDataComponent>(first, out var metaData))
            return;

        var name = _selection.Selected.Count == 1
            ? metaData.EntityName
            : Loc.GetString("rts-selection-group", ("name", metaData.EntityName), ("count", _selection.Selected.Count));

        if (_entManager.TryGetComponent<RtsHealthComponent>(first, out var health))
            screen.Selection.ShowUnit(name, health.Health, health.MaxHealth);
        else
            screen.Selection.ShowUnit(name, 0, 0);
    }

    private void OnScreenLoad()
    {
        if (Screen is not { } screen)
            return;

        screen.Alerts.OnLeavePressed += RequestLeave;
    }

    /// <summary>
    /// Вход в матч и выход из него. Экран перезагружается только при реальной смене
    /// состояния, чтобы не дёргать интерфейс на каждом событии.
    /// </summary>
    /// <param name="reloadScreen">
    /// Выключается при отключении от сервера: там игровое состояние и так разбирается,
    /// а перезагрузка экрана посреди этого только мешает. Флаг при этом обязан сброситься,
    /// иначе после переподключения игрок получит RTS-интерфейс без матча.
    /// </param>
    public void SetInMatch(bool value, bool reloadScreen = true)
    {
        if (IsInMatch == value)
            return;

        IsInMatch = value;

        if (!reloadScreen)
            return;

        if (_state.CurrentState is GameplayState gameplay)
            gameplay.ReloadMainScreen();
    }

    private void RequestLeave()
    {
        _cameras?.RequestLeaveMatch();
    }

    /// <summary>
    /// Зум колесом. Зовётся экраном: колесо мыши нельзя забиндить как обычную клавишу.
    /// </summary>
    public void Zoom(float delta)
    {
        _cameras?.Zoom(delta);
    }
}
