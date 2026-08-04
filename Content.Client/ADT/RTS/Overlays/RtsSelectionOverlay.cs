using System.Numerics;
using Content.Shared.ADT.RTS.Components;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.ADT.RTS.Overlays;

/// <summary>
/// Маркеры выделения, рамка протяжки и полоски здоровья.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §5.2.
/// </summary>
public sealed class RtsSelectionOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private static readonly Color SelectionColor = Color.FromHex("#5CE05C");
    private static readonly Color DragBoxColor = Color.FromHex("#5CE05C60");
    private static readonly Color HealthBackColor = Color.FromHex("#000000AA");
    private static readonly Color OrderLineColor = Color.FromHex("#3ADB3AC0");
    private static readonly Color OrderFillColor = Color.FromHex("#3ADB3A50");

    /// <summary>
    /// Высота полоски здоровья над юнитом в тайлах.
    /// </summary>
    private const float HealthBarOffset = 0.55f;

    private const float HealthBarWidth = 0.7f;
    private const float HealthBarHeight = 0.1f;

    /// <summary>
    /// Длина уголка скобки выделения в долях радиуса маркера.
    /// </summary>
    private const float BracketLength = 0.45f;

    private readonly IEntityManager _entManager;
    private readonly RtsSelectionSystem _selection;
    private readonly SharedTransformSystem _xform;

    public RtsSelectionOverlay(IEntityManager entManager, RtsSelectionSystem selection, SharedTransformSystem xform)
    {
        _entManager = entManager;
        _selection = selection;
        _xform = xform;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        DrawOrders(handle);
        DrawSelectionMarkers(handle, args);
        DrawHealthBars(handle, args);
        DrawDragBox(handle);
    }

    /// <summary>
    /// Точка назначения выделенных юнитов: квадрат на целевом тайле и линия от юнита к нему.
    ///
    /// Рисуем под маркерами выделения, чтобы линии не перечёркивали скобки.
    /// </summary>
    private void DrawOrders(DrawingHandleWorld handle)
    {
        var movementQuery = _entManager.GetEntityQuery<RtsMovementComponent>();

        foreach (var uid in _selection.Selected)
        {
            if (!movementQuery.TryGetComponent(uid, out var movement) || movement.Target is not { } target)
                continue;

            var position = _xform.GetWorldPosition(uid);

            handle.DrawLine(position, target, OrderLineColor);

            // Квадрат рисуем по границам тайла, а не вокруг точки: так сразу видно,
            // на какую именно клетку уйдёт юнит.
            var tile = new Vector2(MathF.Floor(target.X), MathF.Floor(target.Y));
            var box = new Box2(tile, tile + Vector2.One);

            handle.DrawRect(box, OrderFillColor);
            handle.DrawRect(box, OrderLineColor, filled: false);
        }
    }

    private void DrawSelectionMarkers(DrawingHandleWorld handle, in OverlayDrawArgs args)
    {
        var selectableQuery = _entManager.GetEntityQuery<RtsSelectableComponent>();

        foreach (var uid in _selection.Selected)
        {
            if (!selectableQuery.TryGetComponent(uid, out var selectable))
                continue;

            var position = _xform.GetWorldPosition(uid);

            if (!args.WorldAABB.Contains(position))
                continue;

            DrawBracket(handle, position, selectable.MarkerRadius);
        }
    }

    /// <summary>
    /// Четыре уголка вокруг юнита вместо сплошной рамки: так читаемее, когда юнитов много.
    /// </summary>
    private void DrawBracket(DrawingHandleWorld handle, Vector2 position, float radius)
    {
        var length = radius * BracketLength;

        for (var xSign = -1; xSign <= 1; xSign += 2)
        {
            for (var ySign = -1; ySign <= 1; ySign += 2)
            {
                var corner = position + new Vector2(radius * xSign, radius * ySign);

                handle.DrawLine(corner, corner - new Vector2(length * xSign, 0f), SelectionColor);
                handle.DrawLine(corner, corner - new Vector2(0f, length * ySign), SelectionColor);
            }
        }
    }

    private void DrawHealthBars(DrawingHandleWorld handle, in OverlayDrawArgs args)
    {
        var healthQuery = _entManager.GetEntityQuery<RtsHealthComponent>();

        foreach (var uid in _selection.Selected)
        {
            if (!healthQuery.TryGetComponent(uid, out var health) || health.MaxHealth <= 0)
                continue;

            var position = _xform.GetWorldPosition(uid);

            if (!args.WorldAABB.Contains(position))
                continue;

            var origin = position + new Vector2(-HealthBarWidth / 2f, HealthBarOffset);
            var background = Box2.FromDimensions(origin, new Vector2(HealthBarWidth, HealthBarHeight));
            handle.DrawRect(background, HealthBackColor);

            var fraction = Math.Clamp((float) health.Health / health.MaxHealth, 0f, 1f);
            var fill = Box2.FromDimensions(origin, new Vector2(HealthBarWidth * fraction, HealthBarHeight));
            handle.DrawRect(fill, GetHealthColor(fraction));
        }
    }

    private static Color GetHealthColor(float fraction)
    {
        if (fraction > 0.6f)
            return Color.FromHex("#3ADB3A");

        if (fraction > 0.3f)
            return Color.FromHex("#DBD03A");

        return Color.FromHex("#DB3A3A");
    }

    private void DrawDragBox(DrawingHandleWorld handle)
    {
        if (_selection.GetDragBox() is not { } box)
            return;

        handle.DrawRect(box, DragBoxColor);
        handle.DrawRect(box, SelectionColor, filled: false);
    }
}
